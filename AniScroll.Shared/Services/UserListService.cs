using AniScroll.Shared.Models;

namespace AniScroll.Shared.Services
{
    public class UserListService
    {
        private readonly Dictionary<int, UserListEntry> _entries = new();

        // Key = animeId, Value = UTC time it was favourited.
        // AniList doesn't expose individual "favourited at" timestamps,
        // so imported favourites receive a synthetic decreasing timestamp
        // (index 0 = most recently favourited per AniList's ordering).
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

        /// <summary>
        /// Bulk-imports favourites from AniList.
        /// <paramref name="orderedIds"/> must be ordered most-recently-favourited first
        /// (as returned by FetchFavouriteIdsAsync). Existing local favourites whose ID
        /// appears in the list keep their timestamp if it is more recent; otherwise the
        /// imported timestamp wins. IDs not in the list are left untouched.
        /// Call <see cref="NotifyChanged"/> once after calling this.
        /// </summary>
        public void ImportFavorites(IReadOnlyList<int> orderedIds)
        {
            // Assign synthetic timestamps: index 0 → importBase, index 1 → importBase - 1s, …
            // Slightly in the past so any locally-toggled favourite (which uses DateTime.UtcNow)
            // naturally sorts ahead of imported ones after the import.
            var importBase = DateTime.UtcNow.AddSeconds(-1);

            for (int i = 0; i < orderedIds.Count; i++)
            {
                int id = orderedIds[i];
                var importedAt = importBase.AddSeconds(-i);

                if (_favorites.TryGetValue(id, out var existing))
                {
                    if (importedAt > existing)
                        _favorites[id] = importedAt;
                }
                else
                {
                    _favorites[id] = importedAt;
                }
            }
        }

        public IReadOnlyCollection<int> GetFavoriteIds() => _favorites.Keys.ToList();

        public bool IsInList(int animeId) => _entries.ContainsKey(animeId);

        public ListStatus? GetStatus(int animeId) =>
            _entries.TryGetValue(animeId, out var e) ? e.Status : null;

        public UserListEntry? GetEntry(int animeId) =>
            _entries.TryGetValue(animeId, out var e) ? e : null;

        private List<UserCustomList> _customLists = new();

        // ── Status list settings ──────────────────────────────────────────────
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

        public List<UserCustomList> GetCustomLists() =>
            _customLists.OrderBy(l => l.Order).ToList();

        public List<UserCustomList> GetStatusTypeLists() =>
            _customLists.Where(l => l.IsStatusList).OrderBy(l => l.Order).ToList();

        public List<UserCustomList> GetRegularCustomLists() =>
            _customLists.Where(l => !l.IsStatusList).OrderBy(l => l.Order).ToList();

        public void SaveCustomLists(List<UserCustomList> lists)
        {
            _customLists = lists;
            OnChanged?.Invoke();
        }

        // ── Entry CRUD ────────────────────────────────────────────────────────

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

        public void SaveEntry(UserListEntry entry)
        {
            bool isNew = !_entries.TryGetValue(entry.Anime.Id, out var existing);
            if (isNew)
            {
                entry.AddedAt = DateTime.UtcNow;
                entry.UpdatedAt = DateTime.UtcNow;
            }
            else if (HasEntryChanged(existing!, entry))
            {
                entry.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                entry.UpdatedAt = existing!.UpdatedAt;
            }
            _entries[entry.Anime.Id] = entry;
            OnChanged?.Invoke();
        }

        private static bool HasEntryChanged(UserListEntry a, UserListEntry b) =>
            a.Status != b.Status || a.Score != b.Score || a.EpisodesWatched != b.EpisodesWatched ||
            a.TotalRewatches != b.TotalRewatches || a.Notes != b.Notes || a.StartDate != b.StartDate ||
            a.FinishDate != b.FinishDate || a.IsPrivate != b.IsPrivate || a.HideFromStatusLists != b.HideFromStatusLists ||
            !a.CustomListIds.SequenceEqual(b.CustomListIds);

        public void ImportEntry(UserListEntry entry)
        {
            _entries[entry.Anime.Id] = entry;
        }

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

        public void AddOrUpdateToCustomList(AnimeCard anime, string customListId)
        {
            if (!_entries.TryGetValue(anime.Id, out var entry))
            {
                entry = new UserListEntry
                {
                    Anime = anime,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _entries[anime.Id] = entry;
            }
            else
            {
                entry.UpdatedAt = DateTime.UtcNow;
            }

            if (!entry.CustomListIds.Contains(customListId))
                entry.CustomListIds.Add(customListId);

            OnChanged?.Invoke();
        }
    }
}