using System.IO.Compression;
using System.Net.Http.Headers;
using static Facepunch.Constants;

namespace Facepunch.Steps;

/// <summary>
/// Packages the managed assemblies that are required to compile a game with
/// <c>Sandbox.Compiler</c> into a single zip and POSTs it to the Facepunch
/// backend, which stores them as the reference assemblies used for server-side
/// compilation of user code.
/// </summary>
internal class UploadReferenceAssemblies( string name, BuildTarget target = BuildTarget.Staging ) : Step( name )
{
	private const string Endpoint = "https://public.facepunch.com/sbox/internal/reference-assemblies";
	private readonly BuildTarget _target = target;

	/// <summary>
	/// Every assembly user code is compiled against. If any of these are missing the
	/// step fails. Keep this in sync with the reference list in Sandbox.Engine
	/// Project.Compiling.cs.
	/// </summary>
	private static readonly string[] ReferenceAssemblies =
	{
		"game/bin/managed/Sandbox.System.dll",
		"game/bin/managed/Sandbox.Engine.dll",
		"game/bin/managed/Sandbox.Filesystem.dll",
		"game/bin/managed/Sandbox.Reflection.dll",
		"game/bin/managed/Sandbox.Mounting.dll",
		"game/bin/managed/Sandbox.Compiling.dll",
		"game/bin/managed/Sandbox.Bind.dll",
		"game/bin/managed/Sandbox.Tools.dll",
		"game/bin/managed/Sandbox.Event.dll",
		"game/bin/managed/Facepunch.ActionGraphs.dll",
		"game/bin/managed/SkiaSharp.dll",
		"game/bin/managed/Microsoft.CodeAnalysis.dll",
		"game/bin/managed/Microsoft.CodeAnalysis.CSharp.dll",
		"game/bin/managed/Microsoft.AspNetCore.Components.dll",
	};

	protected override ExitCode RunInternal()
	{
		try
		{
			return UploadAsync().GetAwaiter().GetResult();
		}
		catch ( Exception ex )
		{
			Log.Error( $"Reference assembly upload failed with error: {ex}" );
			return ExitCode.Failure;
		}
	}

	private async Task<ExitCode> UploadAsync()
	{
		var key = Environment.GetEnvironmentVariable( "REFERENCE_ASSEMBLY_UPLOAD_KEY" );
		if ( string.IsNullOrWhiteSpace( key ) )
		{
			Log.Error( "REFERENCE_ASSEMBLY_UPLOAD_KEY is not set; cannot upload reference assemblies." );
			return ExitCode.Failure;
		}

		// Collect the assemblies we want to ship as references.
		if ( !TryCollectAssemblies( out var files ) )
			return ExitCode.Failure;

		// Zip them up into a single archive.
		var zipPath = Path.Combine( Path.GetTempPath(), $"reference-assemblies-{Guid.NewGuid():N}.zip" );

		try
		{
			CreateArchive( files, zipPath );

			var zipBytes = await File.ReadAllBytesAsync( zipPath );
			Log.Info( $"Packaged {files.Count} assemblies into archive ({Utility.FormatSize( zipBytes.Length )})" );

			var channel = BuildTargetToSteamBranch( _target );
			var gitRef = GetRef();

			var requestUri = $"{Endpoint}?channel={Uri.EscapeDataString( channel )}&ref={Uri.EscapeDataString( gitRef )}";

			using var http = new HttpClient { Timeout = TimeSpan.FromMinutes( 10 ) };

			using var content = new ByteArrayContent( zipBytes );
			content.Headers.ContentType = new MediaTypeHeaderValue( "application/zip" );

			using var request = new HttpRequestMessage( HttpMethod.Post, requestUri ) { Content = content };
			request.Headers.TryAddWithoutValidation( "Authorization", $"Bearer {key}" );

			Log.Info( $"Uploading reference assemblies (channel: {channel}, ref: {gitRef})..." );

			using var response = await http.SendAsync( request );
			var body = await response.Content.ReadAsStringAsync();

			if ( !response.IsSuccessStatusCode )
			{
				Log.Error( $"Reference assembly upload failed with status {response.StatusCode}: {body}" );
				return ExitCode.Failure;
			}

			Log.Info( "Reference assembly upload completed successfully" );
			return ExitCode.Success;
		}
		finally
		{
			try { if ( File.Exists( zipPath ) ) File.Delete( zipPath ); } catch { }
		}
	}

	/// <summary>
	/// Resolves the list of assemblies to package. Returns false if a required
	/// assembly is missing.
	/// </summary>
	private bool TryCollectAssemblies( out List<string> files )
	{
		files = new List<string>();

		foreach ( var file in ReferenceAssemblies )
		{
			if ( !File.Exists( file ) )
			{
				Log.Error( $"Required reference assembly not found: {file}" );
				return false;
			}

			files.Add( file );
		}

		return true;
	}

	/// <summary>
	/// The identifier this build is stored under. Prefers the git tag name when the
	/// build was triggered by a tag, otherwise the short commit hash. Falls back to a
	/// timestamp for local runs with no git context.
	/// </summary>
	private static string GetRef()
	{
		if ( Environment.GetEnvironmentVariable( "GITHUB_REF" )?.StartsWith( "refs/tags/" ) == true )
		{
			var tag = Environment.GetEnvironmentVariable( "GITHUB_REF_NAME" );
			if ( !string.IsNullOrWhiteSpace( tag ) )
				return tag;
		}

		var sha = Environment.GetEnvironmentVariable( "GITHUB_SHA" );
		if ( !string.IsNullOrWhiteSpace( sha ) )
			return sha[..Math.Min( sha.Length, 7 )];

		return $"local-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
	}

	private static void CreateArchive( IReadOnlyList<string> files, string zipPath )
	{
		if ( File.Exists( zipPath ) )
			File.Delete( zipPath );

		using var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create );

		foreach ( var file in files )
		{
			// Flatten everything to the archive root keyed by file name so the
			// backend doesn't need to know about our build folder layout.
			archive.CreateEntryFromFile( file, Path.GetFileName( file ), CompressionLevel.Optimal );
		}
	}
}
