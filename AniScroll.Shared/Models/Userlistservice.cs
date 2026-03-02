using AniScroll.Shared.Models;

namespace AniScroll.Shared.Services
{
    public class UserListService
    {
        private readonly Dictionary<int, UserListEntry> _entries = new();

        public event Action? OnChanged;

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
                existing.Status    = status;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _entries[anime.Id] = new UserListEntry
                {
                    Anime     = anime,
                    Status    = status,
                    AddedAt   = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
            }
            OnChanged?.Invoke();
        }

        /// <summary>Full update — replaces the entry wholesale (from the list editor).</summary>
        public void SaveEntry(UserListEntry entry)
        {
            entry.UpdatedAt = DateTime.Now;
            if (!_entries.ContainsKey(entry.Anime.Id))
                entry.AddedAt = DateTime.Now;
            _entries[entry.Anime.Id] = entry;
            OnChanged?.Invoke();
        }

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