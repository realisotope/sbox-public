using System;
using System.IO;

namespace Editor;

/// <summary>
/// Manages per-project tag appearance settings (color + optional material icon).
/// Data is stored in the project settings folder as "TagAppearance.json".
/// </summary>
public static class TagAppearanceSettings
{
	/// <summary>
	/// Appearance data for a single tag.
	/// </summary>
	public class TagAppearance
	{
		/// <summary>The display color. Transparent (alpha 0) means "use default auto-color".</summary>
		[Title( "Color" )]
		public Color Color { get; set; } = default;

		/// <summary>Optional Material icon name (e.g. "star", "construction"). Empty means "use default icon".</summary>
		[Title( "Icon" ), IconName]
		public string MaterialIcon { get; set; } = "";

		public bool HasColor => Color.a > 0.001f;
		internal bool HasIcon => !string.IsNullOrWhiteSpace( MaterialIcon );

		public override int GetHashCode() => System.HashCode.Combine( Color, MaterialIcon );
	}

	private static readonly string SettingsFile = "TagAppearance.json";
	private static Dictionary<string, TagAppearance> _all = null;

	/// <summary>
	/// All currently configured tag appearances, keyed by tag name.
	/// </summary>
	public static IReadOnlyDictionary<string, TagAppearance> All
	{
		get
		{
			EnsureLoaded();
			return _all;
		}
	}

	/// <summary>
	/// Get the appearance for a given tag. Always returns a non-null object (may be defaults).
	/// </summary>
	public static TagAppearance GetAppearance( string tag )
	{
		EnsureLoaded();
		if ( _all.TryGetValue( tag, out var appearance ) )
			return appearance;

		var newEntry = new TagAppearance();
		_all[tag] = newEntry;
		return newEntry;
	}

	/// <summary>
	/// Update the appearance for a tag. Call Save() to persist changes to disk.
	/// Also updates AssetTagSystem so the browser refreshes immediately.
	/// </summary>
	public static void SetAppearance( string tag, Color color, string materialIcon )
	{
		EnsureLoaded();

		if ( !_all.TryGetValue( tag, out var entry ) )
		{
			entry = new TagAppearance();
			_all[tag] = entry;
		}

		entry.Color = color;
		entry.MaterialIcon = materialIcon;

		// Push into AssetTagSystem so UI rebuilds reflect the new appearance
		AssetTagSystem.RegisterUserTag( tag, color, materialIcon );
	}

	/// <summary>
	/// Remove an appearance entry (resets to defaults). Call Save() to persist.
	/// </summary>
	public static void RemoveAppearance( string tag )
	{
		EnsureLoaded();
		if ( _all.Remove( tag ) )
		{
			// Reset in tag system too (clear color + icon)
			AssetTagSystem.RegisterUserTag( tag, default, null );
		}
	}

	/// <summary>
	/// Load settings from disk and push all tag appearances into AssetTagSystem.
	/// Call this once at startup (or when a new project opens).
	/// </summary>
	public static void Load( bool force = false )
	{
		if ( force ) _all = null;
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if ( _all is not null )
			return;

		var loaded = FileSystem.ProjectSettings.ReadJsonOrDefault<IEnumerable<KeyValuePair<string, TagAppearance>>>( SettingsFile );
		_all = loaded?.ToDictionary( x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase ) ?? new Dictionary<string, TagAppearance>( StringComparer.OrdinalIgnoreCase );

		// Push all loaded appearances into AssetTagSystem
		foreach ( var (tag, appearance) in _all )
		{
			AssetTagSystem.RegisterUserTag( tag, appearance.Color, appearance.MaterialIcon );
		}
	}

	public static void Save()
	{
		if ( _all is null ) return;

		// Only persist entries that differ from defaults
		var defaultHash = new TagAppearance().GetHashCode();
		var toSave = _all.Where( x => x.Value.GetHashCode() != defaultHash );

		if ( !toSave.Any() && !FileSystem.ProjectSettings.FileExists( SettingsFile ) )
			return;

		FileSystem.ProjectSettings.WriteJson( SettingsFile, toSave );
	}
}
