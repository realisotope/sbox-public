using System;

namespace Editor;

/// <summary>
/// Handles asset tags.
/// </summary>
public static class AssetTagSystem
{
	static AssetTagSystem()
	{
		// Idential to "Show Bullshit Files", so fuck it.
		//RegisterAssetTag( "@child_asset", "Child Asset", "Uninteresting child resource.", "toolimages:assetbrowser/asset_badges/asset_is_trivial_child.png", x => { return x.IsTrivalChild; } );

		// This is an expensive tag that nobody is using
		RegisterAssetTag( "@unreferenced", "Unreferenced", "Assets that are not referenced by other content. Be warned this doesn't account for code/ugc/scripts or shared with other mods.", "toolimages:assetbrowser/asset_badges/asset_unreferenced_by_content.png", x => { return !x.GetReferences( false ).Any(); } );

		RegisterAssetTag( "@nosource", "No Source", "Compiled assets that are missing sources.", "toolimages:assetbrowser/asset_badges/asset_with_no_content.png", x => { return x.HasCompiledFile && !x.HasSourceFile; } );
		RegisterAssetTag( "@published", "Published", $"Assets that have been enabled for publishing to {Global.BackendTitle}.", "toolimages:assetbrowser/copy_list_to_clipboard.png", x => { return x.GetPublishSettings( false )?.Enabled ?? false; } );

		RegisterAssetTag( "@vmat_decal", "Decal Material", "Decal materials, useful in Hammer.", "toolimages:assetbrowser/asset_badges/vmat_decal.png", x => x.FindIntEditInfo( "decal" ) == 1 );
		RegisterAssetTag( "@vmat_overlay", "Overlay Material", "Overlay materials, useful in Hammer.", "toolimages:common/image_sm_bw.png", x => x.FindIntEditInfo( "overlay" ) == 1 );
		RegisterAssetTag( "@vmat_blend", "Blend Material", "Materials that use multi-layer blending, useful in Hammer.", "toolimages:assetbrowser/asset_badges/vmat_blend.png", x => x.FindIntEditInfo( "blendable" ) == 1 );
		RegisterAssetTag( "@vmat_translucent", "Translucent Material", "Translucent materials (alpha-blended).", "toolimages:assetbrowser/asset_badges/vmat_translucent.png", x => x.FindIntEditInfo( "translucent" ) == 1 );
		RegisterAssetTag( "@vmat_alphatest", "Alpha-Tested", "Partially transparent materials (alpha-tested, 1 bit alpha).", "toolimages:assetbrowser/asset_badges/vmat_alphatest.png", x => x.FindIntEditInfo( "alphatest" ) == 1 );
		RegisterAssetTag( "@vmat_tools_nodraw", "Tool Material", "Tools-only materials that will not appear in-game.", "toolimages:assetbrowser/asset_badges/vmat_tools_nodraw.png", x => x.FindIntEditInfo( "mapbuilder.nodraw" ) == 1 );

		//RegisterAssetTag( "@vtex_generated", "Auto Generated", "Textures that were automatically generated.", Color.FromBytes( 255, 255, 255 ), x => x.AssetType == AssetType.Texture && x.Path.IndexOf( ".generated." ) != -1 );
		RegisterAssetTag( "@vmdl_gibs", "Gib Models", "Broken versions of models a.k.a. gibs.", null, x => x.AssetType == AssetType.Model && x.GetDependants( false ).Any( y => y.AssetType == AssetType.Model ) );
		RegisterAssetTag( "@vmat_model", "Model Materials", "Materials used by models.", null, x => x.AssetType == AssetType.Material && x.GetDependants( false ).Any( y => y.AssetType == AssetType.Model ) );

		RegisterAssetTag( "@tex_sheet", "Sheet Texture", "A texture with a sheet", null, x => x.FindIntEditInfo( "sheet" ) > 0 );
		RegisterAssetTag( "@tex_sheet_animated", "Sheet Texture - Animated", "A texture with animation sheets", null, x => x.FindIntEditInfo( "sheet_animations" ) > 0 );
	}

	public delegate bool AssetAutoTagFilter( Asset asset );

	public struct TagDefinition
	{
		public string Tag { get; internal set; }
		public string Title { get; internal set; }
		public string Description { get; internal set; }
		public string Icon { get; internal set; }
		/// <summary>User-configured display color. Transparent means "use auto-color".</summary>
		public Color Color { get; internal set; }
		/// <summary>Optional Material icon name override for the tag's icon pixmap.</summary>
		public string MaterialIcon { get; internal set; }
		public bool AutoTag => Filter != null;
		public AssetAutoTagFilter Filter { get; internal set; }
		public Pixmap IconPixmap => GetTagIcon( Tag );
	}

	static readonly CaseInsensitiveConcurrentDictionary<TagDefinition> TagDefinitions = new();

	static Dictionary<string, Pixmap> TagIcons = new();

	/// <summary>
	/// Get an auto generated icon for given tag.
	/// </summary>
	public static Pixmap GetTagIcon( string tag )
	{
		if ( TagIcons.ContainsKey( tag ) )
			return TagIcons[tag];

		var tagDefs = All.Where( x => x.Tag == tag );
		var tagDef = tagDefs.FirstOrDefault();

		if ( !tagDefs.Any() )
		{
			tagDef.Title = tag.ToTitleCase();
		}

		if ( !string.IsNullOrEmpty( tagDef.Icon ) )
		{
			TagIcons[tag] = Pixmap.FromFile( tagDef.Icon );
			return TagIcons[tag];
		}

		TagIcons[tag] = DrawIcon( tagDef );
		return TagIcons[tag];
	}

	static Color[] tagColors = new[]
	{
		Color.Parse( "#FF70A6" ) ?? default,
		Color.Parse( "#FF707E" ) ?? default,
		Color.Parse( "#E770FF" ) ?? default,
		Color.Parse( "#8370FF" ) ?? default,
		Color.Parse( "#70BAFF" ) ?? default,
		Color.Parse( "#70FFC1" ) ?? default,
		Color.Parse( "#70FF7C" ) ?? default,
		Color.Parse( "#C8FF70" ) ?? default,
		Color.Parse( "#FFF170" ) ?? default,
		Color.Parse( "#FFA270" ) ?? default,
		Color.Parse( "#FF7070" ) ?? default,
	};

	static Pixmap DrawIcon( TagDefinition def )
	{
		// Use user-configured color if set, otherwise use deterministic hash color
		Color color;
		if ( def.Color.a > 0.001f )
		{
			color = def.Color;

			// If the tag has a material icon override, render that instead of a letter square
			if ( !string.IsNullOrWhiteSpace( def.MaterialIcon ) )
			{
				var iconPm = new Pixmap( 16, 16 );
				iconPm.Clear( new Color( 0, 0, 0, 0 ) );
				var ir = new Rect( 0, iconPm.Size );
				using ( Paint.ToPixmap( iconPm ) )
				{
					Paint.Antialiasing = true;
					Paint.TextAntialiasing = true;
					Paint.ClearPen();
					Paint.SetBrush( color.Darken( 0.2f ) );
					Paint.DrawRect( ir, 2 );
					Paint.SetPen( color.Lighten( 0.5f ) );
					Paint.DrawIcon( ir, def.MaterialIcon, 10, TextFlag.Center );
				}
				return iconPm;
			}
		}
		else
		{
			var rand = new Random( (def.Tag ?? def.Title ?? "").FastHash() );
			color = rand.FromArray( tagColors );
		}

		var pNewIcon = new Pixmap( 16, 16 );
		var r = new Rect( 0, pNewIcon.Size );
		pNewIcon.Clear( new Color( 0, 0, 0, 0 ) );

		using ( Paint.ToPixmap( pNewIcon ) )
		{
			Paint.TextAntialiasing = true;
			Paint.Antialiasing = true;
			Paint.ClearPen();
			Paint.SetBrush( color.Darken( 0.2f ) );
			Paint.DrawRect( r, 2 );

			Paint.SetPen( color.Lighten( 0.5f ) );
			Paint.SetDefaultFont( 7, 500 );
			
			var letter = (def.Title ?? def.Tag ?? " ")[..1].ToUpper();
			Paint.DrawText( r.Shrink( 3, 0 ), letter, TextFlag.Center );
		}

		return pNewIcon;
	}

	/// <summary>
	/// List of all registered tags.
	/// </summary>
	public static IReadOnlyCollection<TagDefinition> All => (IReadOnlyCollection<TagDefinition>)TagDefinitions.Values;

	/// <summary>
	/// Register a new asset tag.
	/// </summary>
	internal static void RegisterAssetTag( string tag, string title, string desc = "", string icon = null, AssetAutoTagFilter filter = null )
	{
		TagDefinitions[tag] = new TagDefinition() { Tag = tag, Title = title, Description = desc, Icon = icon, Filter = filter };
	}

	/// <summary>
	/// Register or update a user-defined tag with a custom display color and optional material icon.
	/// Called by the tag appearance settings system when the user configures a tag.
	/// </summary>
	public static void RegisterUserTag( string tag, Color color, string materialIcon = null )
	{
		tag = tag.Trim();
		if ( string.IsNullOrWhiteSpace( tag ) ) return;

		var existing = TagDefinitions.ContainsKey( tag ) ? TagDefinitions[tag] : new TagDefinition { Tag = tag, Title = tag.ToTitleCase() };
		existing.Color = color;
		existing.MaterialIcon = materialIcon;
		TagDefinitions[tag] = existing;

		// Invalidate cached icon so it is regenerated with the new color
		TagIcons.Remove( tag );

		MainThread.Queue( () => EditorEvent.RunInterface<AssetSystem.IEventListener>( x => x.OnAssetTagsChanged() ) );
	}

	/// <summary>
	/// Returns the resolved display color for a tag.
	/// Uses the user-configured color if set, otherwise falls back to the auto-generated hash color.
	/// </summary>
	public static Color GetTagColor( string tag )
	{
		if ( TagDefinitions.TryGetValue( tag, out var def ) && def.Color.a > 0.001f )
			return def.Color;

		// Fall back to the deterministic hash color
		var rand = new Random( tag.FastHash() );
		return rand.FromArray( tagColors );
	}

	/// <summary>
	/// Ensure a user-defined tag is registered, for display in UI.
	/// </summary>
	internal static void EnsureRegistered( string tag )
	{
		tag = tag.Trim();
		if ( string.IsNullOrWhiteSpace( tag ) )
			return;

		if ( TagDefinitions.ContainsKey( tag ) )
			return;

		TagDefinitions[tag] = new TagDefinition() { Tag = tag, Title = tag.ToTitleCase() };

		MainThread.Queue( () => EditorEvent.RunInterface<AssetSystem.IEventListener>( x => x.OnAssetTagsChanged() ) );
	}

	/// <summary>
	/// Return true if this tag is automatically applied
	/// </summary>
	public static bool IsAutoTag( string tag )
	{
		return All.Any( x => x.Tag == tag && x.AutoTag );
	}
}

