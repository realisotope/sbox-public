using System.IO;

namespace Editor;

public class DirectoryEntry : IAssetListEntry
{
	private readonly string Path;
	public DirectoryInfo DirectoryInfo => new DirectoryInfo( Path );

	public string Name { get; private set; }
	public string GetStatusText() => Path;

	public DirectoryEntry( string path )
	{
		Path = path;
		Name = System.IO.Path.GetFileName( Path );
	}

	//
	// Events
	//
	public bool OnDoubleClicked( AssetList list )
	{
		if ( list.Browser is AssetBrowser ab )
			ab.NavigateTo( Path );
		return true;
	}

	//
	// List rendering
	//
	private string GetUniqueIcon() => GetUniqueIcon( Name.ToLowerInvariant() );

	private static string MetadataPath => "Directory.metadata";

	internal static string GetUniqueIcon( string name ) => name.ToLower() switch
	{
		"data" => "description",
		"maps" => "map",
		"materials" => "format_paint",
		"models" => "view_in_ar",
		"prefabs" => "ballot",
		"scenes" => "perm_media",
		"shaders" => "grain",
		"textures" => "texture",
		"fonts" => "text_fields",
		"audio" or "sounds" => "volume_up",
		"animgraphs" => "animation",
		"ui" => "palette",

		"assets" => "category",
		"code" => "code",
		"unittests" => "science",
		"editor" => "hardware",
		"exports" => "open_in_browser",
		"localization" => "language",
		"projectsettings" => "tune",

		_ => null,
	};

	public void DrawText( Rect rect )
	{
		Paint.SetDefaultFont( 7 );
		Paint.ClearPen();
		Paint.SetPen( Theme.Text.WithAlpha( 0.7f ) );

		rect.Position = rect.Position - new Vector2( 0, 4 );

		var strText = Paint.GetElidedText( Name, rect.Width, ElideMode.Right );
		Paint.DrawText( rect, strText, TextFlag.Center | TextFlag.WordWrap );
	}

	public void DrawIcon( Rect rect )
	{
		Paint.ClearBrush();
		Paint.SetPen( Theme.Yellow );
		var folderRect = Paint.DrawIcon( rect, "folder", rect.Width );

		var icon = GetUniqueIcon();
		if ( !string.IsNullOrEmpty( icon ) )
		{
			folderRect.Top += 5f;
			Paint.SetPen( Theme.Yellow.Darken( 0.25f ) );
			Paint.DrawIcon( folderRect, icon, rect.Width / 3f, TextFlag.DontClip | TextFlag.Center );
		}
	}

	public void Delete()
	{
		DirectoryInfo.Delete( true );
	}

	public void Rename( string newName )
	{
		var parentPath = DirectoryInfo.Parent.FullName;
		var newPath = System.IO.Path.Combine( parentPath, newName );

		DirectoryInfo.MoveTo( newPath );
	}

}
