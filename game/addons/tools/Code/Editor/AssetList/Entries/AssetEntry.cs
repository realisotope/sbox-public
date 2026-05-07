using System.IO;
using System.ComponentModel;

namespace Editor;

public class AssetEntry : IAssetListEntry
{
	private readonly Color TypeColor;
	private readonly FileMetadata Metadata;

	public readonly FileInfo FileInfo;
	public readonly string TypeName;
	public Asset Asset { get; private set; }

	public readonly Pixmap IconSmall;
	public string Name => FileInfo.Name;
	public DateTime? LastModified => FileInfo.Exists ? FileInfo.LastWriteTime : null;
	public long? Size => FileInfo.Exists ? FileInfo.Length : null;

	public string GetStatusText() => Asset?.AbsolutePath ?? FileInfo.FullName;

	public string AbsolutePath => Asset?.AbsolutePath ?? FileInfo.FullName;
	public AssetType AssetType => Asset?.AssetType ?? null;
	public bool HasCustomColor => Metadata.Color.a > 0.001f;
	public Color CustomColor => Metadata.Color;
	public bool HasCustomIcon => !string.IsNullOrWhiteSpace( Metadata.Icon );
	public string CustomIcon => Metadata.Icon;
	private Color DisplayColor => HasCustomColor ? Metadata.Color : TypeColor;
	private Color TypeStripColor => (HasCustomColor && Metadata.CustomTypeStripColor) ? Metadata.Color : TypeColor;

	public AssetEntry( Asset asset ) : this( new FileInfo( asset.AbsolutePath ), asset )
	{

	}

	public AssetEntry( FileInfo fileInfo, Asset asset )
	{
		FileInfo = fileInfo;
		Metadata = GetMetadata( fileInfo.FullName );

		var fileExtension = Path.GetExtension( fileInfo.Name );

		if ( asset != null )
		{
			TypeColor = asset.AssetType.Color;
			Asset = asset;

			if ( asset.AssetType.IsGameResource )
			{
				string name = asset.AssetType.FriendlyName;
				if ( name.Contains( "/" ) )
					name = name.Substring( name.LastIndexOf( '/' ) + 1 );

				TypeName = name;
			}
			else
				TypeName = asset.AssetType.FriendlyName;

			IconSmall = asset.AssetType.Icon16;
			return;
		}

		var assetType = AssetType.FromExtension( fileExtension );
		if ( assetType != null )
		{
			TypeColor = assetType.Color;
			TypeName = assetType.FriendlyName;
			IconSmall = assetType.Icon16;
			return;
		}

		IconSmall = Pixmap.FromFile( "common/document_sm.png" );
		TypeName = $"{Path.GetExtension( fileInfo.Name ).ToLower()} file";
		TypeColor = Color.Gray;
	}

	//
	// Events
	//
	public void DrawOverlay( Rect rect )
	{
		Paint.BilinearFiltering = true;

		Paint.ClearPen();
		Paint.SetBrush( DisplayColor );
		var miniIconRect = rect.Shrink( 4 );
		miniIconRect.Width = 16;
		miniIconRect.Height = 16;

		if ( HasCustomIcon )
		{
			Paint.SetPen( DisplayColor );
			Paint.DrawIcon( miniIconRect, CustomIcon, 14, TextFlag.Center );
		}
		else
		{
			Paint.Draw( miniIconRect, IconSmall );
		}

		Paint.BilinearFiltering = false;

		Paint.ClearPen();
		Paint.SetBrush( TypeStripColor );
		var stripRect = rect;
		stripRect.Top = rect.Top + rect.Width - 4;
		stripRect.Left = rect.Left + 4;
		stripRect.Right = rect.Right - 4;
		stripRect.Height = 4;
		Paint.DrawRect( stripRect );
	}

	public void OnScrollEnter()
	{
		if ( Asset is null || Asset.HasCachedThumbnail )
			return;

		EditorEvent.Register( this );
		Asset.GetAssetThumb( true );
	}
	public void OnScrollExit()
	{
		if ( Asset is null )
			return;

		Asset.CancelThumbBuild();
		EditorEvent.Unregister( this );
	}

	public void DrawIcon( Rect rect )
	{
		if ( HasCustomIcon )
		{
			Paint.ClearPen();
			Paint.SetBrush( DisplayColor.WithAlpha( 0.14f ) );
			Paint.DrawRect( rect, Theme.ControlRadius );

			Paint.SetPen( DisplayColor.WithAlpha( 0.95f ) );
			var iconRect = rect.Shrink( rect.Width * 0.2f );
			Paint.DrawIcon( iconRect, CustomIcon, rect.Width * 0.55f, TextFlag.Center );
			return;
		}

		var iconLarge = Asset?.GetAssetThumb( true );
		iconLarge ??= GenericTypeIcons.GetForFile( FileInfo.ToString() );

		if ( iconLarge is null )
			return;

		Paint.BilinearFiltering = true;
		Paint.ClearPen();

		var aPos = rect.TopLeft;
		var bPos = rect.BottomLeft;

		var aColor = DisplayColor.WithAlpha( 0 );
		var bColor = DisplayColor.WithAlpha( 0.5f );

		Paint.SetBrushLinear( aPos, bPos, aColor, bColor );
		Paint.DrawRect( rect );
		Paint.Draw( rect, iconLarge );

		Paint.BilinearFiltering = false;
	}

	public void DrawText( Rect rect )
	{
		Paint.SetDefaultFont( 7 );
		Paint.ClearPen();
		Paint.SetPen( HasCustomColor ? Metadata.Color.WithAlpha( 0.85f ) : Theme.Text.WithAlpha( 0.7f ) );

		rect.Top += 2; // Pull down to avoid conflicting with asset type strip

		var strText = Path.GetFileNameWithoutExtension( Name );
		strText = Paint.GetElidedText( strText, rect.Width, ElideMode.Middle );

		Paint.DrawText( rect, strText, TextFlag.LeftTop );

		rect.Top += 12;
		Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );
		Paint.DrawText( rect, TypeName, TextFlag.LeftTop );
	}

	public void Delete()
	{
		var oldPath = FileInfo.FullName;

		if ( Asset is not null )
			Asset.Delete();
		else
			FileInfo.Delete();

		RemoveMetadata( oldPath );
	}

	public void Rename( string newName )
	{
		var oldPath = FileInfo.FullName;
		string compiledPath = Asset?.GetCompiledFile( true );
		if ( !string.IsNullOrEmpty( compiledPath ) )
		{
			var compiled = new FileInfo( compiledPath );
			compiled.MoveTo( compiled.GetNewPath( $"{newName}_c" ) );

			var blob = new FileInfo( $"{compiledPath[..^2]}_d" );
			if ( blob.Exists )
				blob.MoveTo( compiled.GetNewPath( $"{newName}_d" ) );
		}

		FileInfo.MoveTo( FileInfo.GetNewPath( newName ) );
		RenameMetadata( oldPath, FileInfo.FullName );
		Asset = AssetSystem.RegisterFile( FileInfo.FullName );
	}

	public void Duplicate( string newName = null )
	{
		var newPath = FileInfo.GetNewPath( newName ?? FileInfo.GetDefaultDuplicateName() );
		FileInfo.CopyTo( newPath );
		CopyMetadata( FileInfo.FullName, newPath );
	}

	public bool OnDoubleClicked( AssetList list )
	{
		if ( Asset is not null )
		{
			if ( list.Browser is AssetBrowser browser )
				browser.OnAssetSelected?.Invoke( Asset );
		}
		else
		{
			if ( EditorUtility.IsCodeFile( FileInfo.FullName ) )
				CodeEditor.OpenFile( FileInfo.FullName );
			else if ( list.Browser is AssetBrowser browser )
				browser.OnFileSelected?.Invoke( FileInfo.FullName );
		}

		return true;
	}

	public bool OnClicked( AssetList list )
	{
		return true;
	}

	public override bool Equals( object obj )
	{
		if ( obj is not AssetEntry ae )
			return false;

		return FileInfo.FullName.Equals( ae.FileInfo.FullName );
	}

	public override int GetHashCode()
	{
		return FileInfo.FullName.GetHashCode();
	}

	static Dictionary<string, FileMetadata> AllMetadata = null;
	private static string MetadataPath => "File.metadata";

	internal static FileMetadata GetMetadata( string path )
	{
		var relativePath = GetRelativePath( path );

		if ( AllMetadata is null )
		{
			var loadedMetadata = FileSystem.ProjectSettings.ReadJsonOrDefault<IEnumerable<KeyValuePair<string, FileMetadata>>>( MetadataPath );
			AllMetadata = loadedMetadata?.ToDictionary( x => x.Key, x => x.Value ) ?? new Dictionary<string, FileMetadata>();
		}

		if ( AllMetadata.TryGetValue( relativePath, out var metadata ) )
			return metadata;

		var newData = new FileMetadata();
		AllMetadata[relativePath] = newData;
		return newData;
	}

	internal static void RenameMetadata( string path, string newPath )
	{
		if ( AllMetadata is null )
			return;

		var relativePath = GetRelativePath( path );
		var newRelativePath = GetRelativePath( newPath );
		if ( !AllMetadata.TryGetValue( relativePath, out var metadata ) )
			return;

		AllMetadata[newRelativePath] = metadata;
		AllMetadata.Remove( relativePath );
		SaveMetadata();
	}

	internal static void CopyMetadata( string path, string newPath )
	{
		var source = GetMetadata( path );
		if ( source.GetHashCode() == new FileMetadata().GetHashCode() )
			return;

		var target = GetMetadata( newPath );
		target.Color = source.Color;
		target.Icon = source.Icon;
		target.CustomTypeStripColor = source.CustomTypeStripColor;
		SaveMetadata();
	}

	internal static void RemoveMetadata( string path )
	{
		if ( AllMetadata is null )
			return;

		var relativePath = GetRelativePath( path );
		if ( AllMetadata.Remove( relativePath ) )
			SaveMetadata();
	}

	internal static void SaveMetadata()
	{
		if ( AllMetadata is null )
			return;

		var newDataHash = new FileMetadata().GetHashCode();
		var savedMetadata = AllMetadata.Where( x => x.Value.GetHashCode() != newDataHash );

		if ( !savedMetadata.Any() && !FileSystem.ProjectSettings.FileExists( MetadataPath ) )
			return;

		FileSystem.ProjectSettings.WriteJson( MetadataPath, savedMetadata );
	}

	static string GetRelativePath( string path )
	{
		var rootPath = Project.Current.GetRootPath();
		return System.IO.Path.GetRelativePath( rootPath, path );
	}

	internal class FileMetadata
	{
		public Color Color { get; set; } = Theme.Yellow.WithAlpha( 0 );

		[IconName]
		public string Icon { get; set; } = "";

		public bool CustomTypeStripColor { get; set; } = false;

		public override int GetHashCode()
		{
			return System.HashCode.Combine( Color, Icon, CustomTypeStripColor );
		}
	}
}


static class GenericTypeIcons
{
	private readonly static Dictionary<string, Pixmap> _cache = new();

	public static Pixmap GetForFile( string filepath )
	{
		var fileExtension = System.IO.Path.GetExtension( filepath ).ToLowerInvariant();

		if ( fileExtension.StartsWith( "." ) )
			fileExtension = fileExtension[1..];

		if ( _cache.TryGetValue( fileExtension, out var cachedIcon ) )
			return cachedIcon;

		var size = 128;
		var pm = new Pixmap( size, size );
		var rect = new Rect( 0, 0, size, size );

		using ( Paint.ToPixmap( pm ) )
		{
			Paint.ClearBrush();
			Paint.SetPen( Theme.Border );
			Paint.DrawIcon( rect, "insert_drive_file", rect.Width );
		}

		_cache[fileExtension] = pm;

		return pm;
	}
}
