using System.IO;

namespace Editor;

public class FileMetadataDialog : Dialog
{
	Label FileIcon;

	public FileMetadataDialog( FileInfo file )
	{
		Layout = Layout.Column();
		Layout.Margin = 8f;
		Layout.Spacing = 8f;

		var metadata = AssetEntry.GetMetadata( file.FullName );

		var header = Layout.Add( Layout.Row() );
		header.Margin = 4f;
		header.Spacing = 8f;

		FileIcon = header.Add( new Label( this ) );
		FileIcon.Text = "insert_drive_file";
		UpdateIconColor( metadata );

		var fileName = header.Add( new LineEdit( this ) );
		fileName.Text = file.Name;
		fileName.Enabled = false;

		Layout.Add( new Separator( 1f ) );

		var infoColumn = Layout.Add( Layout.Column() );
		infoColumn.Margin = 8f;
		infoColumn.Spacing = 12f;

		var locationRow = infoColumn.Add( Layout.Row() );
		locationRow.Spacing = 8f;
		locationRow.Add( new Label( this ) { Text = "Location:", FixedWidth = 80 } );
		locationRow.Add( new Label( this ) { Text = file.FullName } );

		Layout.Add( new Separator( 1f ) );

		var sheet = new ControlSheet();
		sheet.Spacing = 4f;
		var serialized = metadata.GetSerialized();
		foreach ( var prop in serialized )
		{
			sheet.AddRow( prop );
		}
		Layout.Add( sheet );

		serialized.OnPropertyChanged += _ =>
		{
			UpdateIconColor( metadata );
		};

		Layout.AddStretchCell();

		Window.Size = new Vector2( 500, 225 );
		Window.WindowTitle = $"{file.Name} Metadata";
	}

	private void UpdateIconColor( AssetEntry.FileMetadata metadata )
	{
		var color = metadata.Color.a > 0.001f ? metadata.Color : Theme.Text.WithAlpha( 0.7f );
		var icon = string.IsNullOrWhiteSpace( metadata.Icon ) ? "insert_drive_file" : metadata.Icon;
		FileIcon.Text = icon;
		FileIcon.SetStyles( $"font-family: Material Icons; font-size: 42px; color: {color.Hex};" );
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();
		AssetEntry.SaveMetadata();
	}
}
