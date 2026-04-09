using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RegexTester.ViewModels;

public class RegexToolboxCategory : ObservableObject
{
    private string _id = "";
    private bool _isActive;
    private string _label = "";

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string IconPathData { get; set; } = "";
}

public class RegexToolboxToken : ObservableObject
{
    private string _category = "";
    private string _id = "";
    private string _label = "";
    private string _syntax = "";
    private string _tooltip = "";

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonPropertyName("category")]
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    [JsonPropertyName("label")]
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    [JsonPropertyName("syntax")]
    public string Syntax
    {
        get => _syntax;
        set => SetProperty(ref _syntax, value);
    }

    [JsonPropertyName("tooltip")]
    public string Tooltip
    {
        get => _tooltip;
        set => SetProperty(ref _tooltip, value);
    }
}

public partial class RegexToolboxViewModel : ObservableObject
{
    private const string AllTokensId = "all-tokens";
    private const string AllTokensLabel = "All Tokens";

    private static readonly (string Id, string Label, string IconPathData)[] CategoryBlueprint =
    [
        (AllTokensId, AllTokensLabel, "M16 6 L20 20 M12 6 L12 20 M8 8 L8 20 M4 4 L4 20"),
        ("common-tokens", "Common Tokens",
         "M12 2.8 L14.3 7.5 L19.5 8.3 L15.7 12 L16.6 17.2 L12 14.8 L7.4 17.2 L8.3 12 L4.5 8.3 L9.7 7.5 Z"),
        ("general-tokens", "General Tokens",
         "M16 12 H3 M16 18 H3 M10 6 H3 M21 18 V8 C21 6.9 20.1 6 19 6 H14 M16 8 L14 6 L16 4"),
        ("anchors", "Anchors", "M12 22 V8 M5 12 H2 A10 10 0 0 0 22 12 H19 M12 2 A3 3 0 1 1 12 8 A3 3 0 1 1 12 2"),
        ("meta-sequences", "Meta Sequences",
         "M12 2 A10 10 0 1 1 12 22 A10 10 0 1 1 12 2 M12 11 A1 1 0 1 1 12 13 A1 1 0 1 1 12 11"),
        ("quantifiers", "Quantifiers",
         "M8 3 H7 C5.9 3 5 3.9 5 5 V10 C5 11.1 4.1 12 3 12 C4.1 12 5 12.9 5 14 V19 C5 20.1 5.9 21 7 21 H8 M16 3 H17 C18.1 3 19 3.9 19 5 V10 C19 11.1 19.9 12 21 12 C19.9 12 19 12.9 19 14 V19 C19 20.1 18.1 21 17 21 H16"),
        ("group-constructs", "Group Constructs", "M8 21 C4 18 4 6 8 3 M16 3 C20 6 20 18 16 21"),
        ("character-classes", "Character Classes", "M16 3 H19 V21 H16 M8 21 H5 V3 H8"),
        ("flags-modifiers", "Flags/Modifiers",
         "M4 15 C5 14 8 14 11 15 C14 16 17 16 20 15 V3 C17 4 14 4 11 3 C8 2 5 2 4 3 Z M4 15 V22"),
        ("substitution", "Substitution",
         "M14 4 A2 2 0 0 1 16 2 M16 10 A2 2 0 0 1 14 8 M20 2 A2 2 0 0 1 22 4 M22 8 A2 2 0 0 1 20 10 M3 7 L6 10 L9 7 M6 10 V5 C6 3.3 7.3 2 9 2 H10 M2 14 H10 V22 H2 Z")
    ];

    [ObservableProperty]
    private ObservableCollection<RegexToolboxCategory> categories = new();

    [ObservableProperty]
    private ObservableCollection<RegexToolboxToken> filteredTokens = new();

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private RegexToolboxCategory? selectedCategory;

    [ObservableProperty]
    private ObservableCollection<RegexToolboxToken> tokens = new();

    public RegexToolboxViewModel()
    {
        LoadTokens();

        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
            SelectedCategory.IsActive = true;
        }

        FilterTokens();
    }

    private void LoadTokens()
    {
        string? json = null;

        try
        {
            var assetUri = new Uri("avares://RegexTester/Assets/regex-toolbox-items.json");
            using var stream = AssetLoader.Open(assetUri);
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch
        {
            // Fallback for local debug runs when the asset loader path is unavailable.
            string[] fallbackPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "regex-toolbox-items.json"),
                Path.Combine(Environment.CurrentDirectory, "Assets", "regex-toolbox-items.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "regex-toolbox-items.json"),
                "Assets/regex-toolbox-items.json"
            };

            string? jsonPath = fallbackPaths.FirstOrDefault(File.Exists);
            if (jsonPath != null)
            {
                json = File.ReadAllText(jsonPath);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new Exception("Failed to load regex-toolbox-items.json");
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            List<RegexToolboxToken> items = JsonSerializer.Deserialize<List<RegexToolboxToken>>(json, options) ?? [];
            Tokens.Clear();

            foreach (var t in items)
            {
                if (string.IsNullOrWhiteSpace(t.Label) || string.IsNullOrWhiteSpace(t.Syntax))
                {
                    continue;
                }

                Tokens.Add(t);
            }

            var availableCategoryIds = Tokens
                                       .Select(t => t.Category)
                                       .Where(id => !string.IsNullOrWhiteSpace(id))
                                       .Distinct(StringComparer.OrdinalIgnoreCase)
                                       .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Categories.Clear();

            foreach ((string id, string label, string iconPathData) in CategoryBlueprint)
            {
                if (id == AllTokensId || availableCategoryIds.Contains(id))
                {
                    Categories.Add(new RegexToolboxCategory
                    {
                        Id = id,
                        Label = label,
                        IconPathData = iconPathData
                    });
                }
            }
        }
        catch
        {
            Tokens.Clear();
            Categories.Clear();
        }
    }

    [RelayCommand]
    public void SelectCategory(RegexToolboxCategory? cat)
    {
        if (cat == null)
        {
            return;
        }

        foreach (var c in Categories)
        {
            c.IsActive = c == cat;
        }

        SelectedCategory = cat;
        FilterTokens();
    }

    [RelayCommand]
    public void InsertToken(string? syntax)
    {
        // The actual insertion is handled in the view's code-behind since it needs to interact with the editor control directly.
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterTokens();
    }

    partial void OnSelectedCategoryChanged(RegexToolboxCategory? oldValue, RegexToolboxCategory? newValue)
    {
        foreach (var category in Categories)
        {
            category.IsActive = category == newValue;
        }

        FilterTokens();
    }

    private void FilterTokens()
    {
        FilteredTokens.Clear();

        bool filterByCategory = SelectedCategory != null &&
                                !string.Equals(SelectedCategory.Id, AllTokensId, StringComparison.OrdinalIgnoreCase);

        IEnumerable<RegexToolboxToken> filtered = Tokens.Where(t =>
                                                                   (!filterByCategory ||
                                                                    t.Category == SelectedCategory!.Id) &&
                                                                   (string.IsNullOrWhiteSpace(SearchText) ||
                                                                    t.Label.Contains(SearchText,
                                                                     StringComparison.OrdinalIgnoreCase) ||
                                                                    t.Tooltip.Contains(SearchText,
                                                                     StringComparison.OrdinalIgnoreCase) ||
                                                                    t.Syntax.Contains(SearchText,
                                                                     StringComparison.OrdinalIgnoreCase)));

        foreach (var t in filtered)
        {
            FilteredTokens.Add(t);
        }
    }
}
