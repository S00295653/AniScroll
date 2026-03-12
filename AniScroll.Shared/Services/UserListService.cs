using AniScroll.Shared.Models;

namespace AniScroll.Shared.Services
{
    public class UserListService
    {
        private readonly Dictionary<int, UserListEntry> _entries = new();

        // Key = animeId, Value = UTC timestamp when it was favourited
        // (most recently favourited → highest DateTime)
        private readonly Dictionary<int, DateTime> _favorites = new();

        public event Action? OnChanged;

        // ── Favorites ─────────────────────────────────────────────────────────

        public bool IsFavorited(int animeId) => _favorites.ContainsKey(animeId);

        /// <summary>Returns the UTC time at which this anime was favourited, or MinValue.</summary>
        public DateTime GetFavoritedAt(int animeId) =>
            _favorites.TryGetValue(animeId, out var dt) ? dt : DateTime.MinValue;

        /// <summary>Toggles the favourite state. Returns the new state.</summary>
        public bool ToggleFavorite(int animeId)
        {
            bool isFav;
            if (_favorites.ContainsKey(animeId))
            {
                _favorites.Remove(animeId);
                isFav = false;
            }
            else
            {
                _favorites[animeId] = DateTime.UtcNow;
                isFav = true;
            }
            OnChanged?.Invoke();
            return isFav;
        }

        public IReadOnlyCollection<int> GetFavoriteIds() => _favorites.Keys.ToList();

        public bool IsInList(int animeId) => _entries.ContainsKey(animeId);

        public ListStatus? GetStatus(int animeId) =>
            _entries.TryGetValue(animeId, out var e) ? e.Status : null;

        public UserListEntry? GetEntry(int animeId) =>
            _entries.TryGetValue(animeId, out var e) ? e : null;

        private List<UserCustomList> _customLists = new();

        // ── Status list settings (color + display name, user-editable) ────────
        private List<StatusListSetting> _statusSettings = GetDefaultStatusSettings();

        private static List<StatusListSetting> GetDefaultStatusSettings() => new()
        {
            new() { Key = "Watching",   DisplayName = "Watching",   Color = "#22c55e", Order = 0 },
            new() { Key = "Rewatching", DisplayName = "Rewatching", Color = "#06b6d4", Order = 1 },
            new() { Key = "Completed",  DisplayName = "Completed",  Color = "#3b82f6", Order = 2 },
            new() { Key = "Planning",   DisplayName = "Planning",   Color = "#a855f7", Order = 3 },
            new() { Key = "Paused",     DisplayName = "Paused",     Color = "#f97316", Order = 4 },
            new() { Key = "Dropped",    DisplayName = "Dropped",    Color = "#ef4444", Order = 5 },
        };

        public List<StatusListSetting> GetStatusSettings() =>
            _statusSettings.OrderBy(s => s.Order).ToList();

        public void SaveStatusSettings(List<StatusListSetting> settings)
        {
            _statusSettings = settings;
            OnChanged?.Invoke();
        }

        // ── Custom list accessors ─────────────────────────────────────────────

        /// <summary>Toutes les listes (status-type + regular), triées par Order.</summary>
        public List<UserCustomList> GetCustomLists() =>
            _customLists.OrderBy(l => l.Order).ToList();

        /// <summary>Listes de type "Status" (IsStatusList=true), triées par Order.</summary>
        public List<UserCustomList> GetStatusTypeLists() =>
            _customLists.Where(l => l.IsStatusList).OrderBy(l => l.Order).ToList();

        /// <summary>Listes custom normales (IsStatusList=false), triées par Order.</summary>
        public List<UserCustomList> GetRegularCustomLists() =>
            _customLists.Where(l => !l.IsStatusList).OrderBy(l => l.Order).ToList();

        public void SaveCustomLists(List<UserCustomList> lists)
        {
            _customLists = lists;
            OnChanged?.Invoke();
        }

        // ── Entry CRUD ────────────────────────────────────────────────────────

        /// <summary>Quick add/update — only changes Status, preserves other fields.</summary>
        public void AddOrUpdate(AnimeCard anime, ListStatus status)
        {
            if (_entries.TryGetValue(anime.Id, out var existing))
            {
                existing.Status = status;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _entries[anime.Id] = new UserListEntry
                {
                    Anime = anime,
                    Status = status,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            OnChanged?.Invoke();
        }

        /// <summary>Full update — replaces the entry wholesale (from the list editor).</summary>
        public void SaveEntry(UserListEntry entry)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            if (!_entries.ContainsKey(entry.Anime.Id))
                entry.AddedAt = DateTime.UtcNow;
            _entries[entry.Anime.Id] = entry;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Import from AniList — preserves the original AniList timestamps
        /// (UpdatedAt / AddedAt) instead of overwriting with DateTime.UtcNow.
        /// Call <see cref="NotifyChanged"/> once after the import loop.
        /// </summary>
        public void ImportEntry(UserListEntry entry)
        {
            _entries[entry.Anime.Id] = entry;
        }

        /// <summary>
        /// Fires OnChanged manually — call once after a batch of
        /// <see cref="ImportEntry"/> calls to avoid redundant UI refreshes.
        /// </summary>
        public void NotifyChanged() => OnChanged?.Invoke();

        public void Remove(int animeId)
        {
            if (_entries.Remove(animeId))
                OnChanged?.Invoke();
        }

        public IReadOnlyList<UserListEntry> GetAll() =>
            _entries.Values.OrderByDescending(e => e.UpdatedAt).ToList();

        public IReadOnlyList<UserListEntry> GetByStatus(ListStatus status) =>
            _entries.Values
                    .Where(e => e.Status == status)
                    .OrderByDescending(e => e.UpdatedAt)
                    .ToList();

        public int Count(ListStatus? status = null) =>
            status == null
                ? _entries.Count
                : _entries.Values.Count(e => e.Status == status);

        /// <summary>
        /// Count of entries that are actually tracked (have a status or belong to at
        /// least one custom list). This is what the "My Lists" badge should show —
        /// it excludes entries created purely as AniList stubs with no local status.
        /// </summary>
        public int CountTracked() =>
            _entries.Values.Count(e => e.Status.HasValue || e.CustomListIds.Any());

        // ── Custom list entry helpers ─────────────────────────────────────────

        public List<UserListEntry> GetEntriesForCustomList(string id) =>
            _entries.Values
                .Where(e => e.CustomListIds.Contains(id))
                .OrderByDescending(e => e.UpdatedAt)
                .ToList();

        public int GetCountForCustomList(string id) =>
            _entries.Values.Count(e => e.CustomListIds.Contains(id));

        public bool IsInCustomList(int animeId, string customListId) =>
            _entries.TryGetValue(animeId, out var e) && e.CustomListIds.Contains(customListId);

        public void AddToCustomList(int animeId, string customListId)
        {
            if (!_entries.TryGetValue(animeId, out var e)) return;
            if (!e.CustomListIds.Contains(customListId))
            {
                e.CustomListIds.Add(customListId);
                OnChanged?.Invoke();
            }
        }

        public void RemoveFromCustomList(int animeId, string customListId)
        {
            if (!_entries.TryGetValue(animeId, out var e)) return;
            if (e.CustomListIds.Remove(customListId))
                OnChanged?.Invoke();
        }
    }
}