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
    }
}