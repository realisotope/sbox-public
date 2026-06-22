using System;

namespace Editor.ProjectSettingPages;

/// <summary>
/// Project Settings page that lets users configure per-project tag appearance:
/// color and optional material icon. Changes apply live to the asset browser.
/// </summary>
[Title( "Asset Tags" ), Icon( "bookmark" )]
internal sealed class AssetTagsCategory : ProjectSettingsWindow.Category
{
	private Layout _listLayout;

	public override void OnInit( Project project )
	{
		base.OnInit( project );

		// Load fresh from disk for this project, discarding any unsaved changes in memory
		// (e.g. if the user clicked 'Revert Changes' or switched tabs without saving)
		TagAppearanceSettings.Load( force: true );

		BodyLayout.Add( new Label.Body(
			"Configure custom colors and icons for asset tags. " +
			"Any asset with a matching tag will inherit the tag's color and icon in the browser." ) );

		BodyLayout.AddSpacingCell( 8 );

		// List of tag rows
		_listLayout = BodyLayout.AddColumn();
		_listLayout.Spacing = 4;

		RefreshRows();

		BodyLayout.AddSpacingCell( 4 );
		BodyLayout.Add( new Separator( 1f ) );
		BodyLayout.AddSpacingCell( 4 );

		// "Add Tag" input row at the bottom
		var addRow = BodyLayout.Add( Layout.Row() );
		addRow.Spacing = 8;

		var tagInput = addRow.Add( new LineEdit( this ) );
		tagInput.PlaceholderText = "New tag name...";
		tagInput.FixedWidth = 200;

		var addBtn = addRow.Add( new Button.Primary( "Add Tag", "add" ) );
		addBtn.Clicked = () =>
		{
			var tag = tagInput.Text.Trim().ToLower();
			if ( string.IsNullOrWhiteSpace( tag ) ) return;

			// Ensure it's known to the tag system and appearance settings
			AssetTagSystem.RegisterUserTag( tag, default, null );
			TagAppearanceSettings.GetAppearance( tag );

			tagInput.Text = "";
			RefreshRows();
		};

		tagInput.ReturnPressed += () => addBtn.Clicked?.Invoke();

		addRow.AddStretchCell();
	}

	private void RefreshRows()
	{
		_listLayout.Clear( true );

		// Collect all known user tags (non-auto from AssetTagSystem + any in TagAppearanceSettings)
		var userTags = AssetTagSystem.All
			.Where( x => !x.AutoTag )
			.Select( x => x.Tag )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		foreach ( var tag in TagAppearanceSettings.All.Keys )
			userTags.Add( tag );

		var sorted = userTags.OrderBy( x => x ).ToList();

		if ( sorted.Count == 0 )
		{
			var hint = new Label( "No user tags yet. Right-click an asset → Tags → add a tag, or use \"Add Tag\" below." );
			hint.WordWrap = true;
			_listLayout.Add( hint );
			return;
		}

		foreach ( var tag in sorted )
		{
			_listLayout.Add( new TagRowWidget( this, tag, RefreshRows, () => StateHasChanged() ) );
		}
	}

	public override void OnSave()
	{
		// Persist changes to disk when the user clicks the "Save" button in the settings window
		TagAppearanceSettings.Save();
		base.OnSave();
	}
}

/// <summary>
/// One row in the Asset Tags settings page: tag name label, ControlSheet for color+icon, and a reset button.
/// </summary>
internal class TagRowWidget : Widget
{
	private readonly string _tag;
	private readonly Action _onReset;
	private readonly Action _onChanged;

	public TagRowWidget( Widget parent, string tag, Action onReset, Action onChanged ) : base( parent )
	{
		_tag = tag;
		_onReset = onReset;
		_onChanged = onChanged;

		Layout = Layout.Column();
		Layout.Spacing = 0;

		// Header row: tag name + reset button
		var header = Layout.Add( Layout.Row() );
		header.Spacing = 8;

		// Coloured dot using the tag's current auto-icon as a small pixmap preview
		var nameLabel = header.Add( new Label( this ) );
		nameLabel.Text = tag;
		nameLabel.ToolTip = $"Tag: \"{tag}\"";

		header.AddStretchCell();

		var resetBtn = header.Add( new Button( "Reset", "clear", this ) );
		resetBtn.ToolTip = "Remove custom color and icon (revert to defaults)";
		resetBtn.Clicked = () =>
		{
			TagAppearanceSettings.RemoveAppearance( tag );
			_onChanged?.Invoke();
			_onReset?.Invoke();

		};

		// Property sheet — auto-generates color picker and icon name input
		var sheet = new ControlSheet();
		sheet.Spacing = 2;

		var appearance = TagAppearanceSettings.GetAppearance( tag );
		var serialized = appearance.GetSerialized();

		foreach ( var prop in serialized )
		{
			sheet.AddRow( prop );
		}

		// When any property changes, push the updated values into the settings
		serialized.OnPropertyChanged += _ =>
		{
			TagAppearanceSettings.SetAppearance( tag, appearance.Color, appearance.MaterialIcon );
			_onChanged?.Invoke();
		};

		Layout.Add( sheet );
		Layout.Add( new Separator( 1f ) );
	}
}
