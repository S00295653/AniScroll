using AniScroll.Shared.Models;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace AniScroll.Shared.Helpers;

/// <summary>
/// Helpers UI partagés entre tous les composants Razor.
/// Regroupe les méthodes statiques dupliquées dans AnimeDetailPopup,
/// ListsPanel et CustomListManager.
/// </summary>
public static class UiHelpers
{
    // ── Status : labels & icônes ──────────────────────────────────────────────

    public static string GetStatusLabel(ListStatus s) => s switch
    {
        ListStatus.Watching   => "Watching",
        ListStatus.Rewatching => "Rewatching",
        ListStatus.Completed  => "Completed",
        ListStatus.Planning   => "Planning",
        ListStatus.Paused     => "Paused",
        ListStatus.Dropped    => "Dropped",
        _                     => s.ToString()
    };

    public static string GetStatusIcon(ListStatus s) => s switch
    {
        ListStatus.Watching   => "▶",
        ListStatus.Rewatching => "↻",
        ListStatus.Completed  => "✓",
        ListStatus.Planning   => "☆",
        ListStatus.Paused     => "⏸",
        ListStatus.Dropped    => "✕",
        _                     => ""
    };

    public static string GetStatusCssKey(ListStatus s) => s switch
    {
        ListStatus.Watching   => "watching",
        ListStatus.Rewatching => "rewatching",
        ListStatus.Completed  => "completed",
        ListStatus.Planning   => "planning",
        ListStatus.Paused     => "paused",
        ListStatus.Dropped    => "dropped",
        _                     => "all"
    };

    // ── Status : couleurs par défaut (fallback sans réglage utilisateur) ──────

    public static string GetDefaultStatusColor(ListStatus s) => s switch
    {
        ListStatus.Watching   => "#22c55e",
        ListStatus.Rewatching => "#06b6d4",
        ListStatus.Completed  => "#3b82f6",
        ListStatus.Planning   => "#a855f7",
        ListStatus.Paused     => "#f97316",
        ListStatus.Dropped    => "#ef4444",
        _                     => "#6366f1"
    };

    /// <summary>
    /// Retourne la couleur du statut : couleur perso si configurée,
    /// sinon couleur par défaut.
    /// </summary>
    public static string GetStatusColor(ListStatus s,
        IEnumerable<StatusListSetting>? userSettings = null)
    {
        if (userSettings != null)
        {
            var found = userSettings.FirstOrDefault(x => x.Key == s.ToString());
            if (found != null) return found.Color;
        }
        return GetDefaultStatusColor(s);
    }

    // ── Score : couleurs et formatage ─────────────────────────────────────────

    public static string GetScoreHexColor(double score) => score switch
    {
        <= 0 => "#3f3f58",
        <= 2 => "#ef4444",
        <= 4 => "#f97316",
        <= 6 => "#eab308",
        <= 8 => "#84cc16",
        _    => "#22c55e"
    };

    public static string GetScoreColorClass(double score) => score switch
    {
        <= 2 => "score-bar-red",
        <= 4 => "score-bar-orange",
        <= 6 => "score-bar-yellow",
        <= 8 => "score-bar-lime",
        _    => "score-bar-green"
    };

    public static string FormatScore(double score)
    {
        if (score <= 0) return "";
        return score % 1 == 0
            ? ((int)score).ToString()
            : score.ToString("0.0", CultureInfo.InvariantCulture);
    }

    // ── Markup helpers ────────────────────────────────────────────────────────

    /// <summary>Point coloré avec effet glow — utilisé dans les dropdowns de status.</summary>
    public static MarkupString GlowDot(string color)
    {
        var style = $"display:inline-block;width:8px;height:8px;border-radius:50%;flex-shrink:0;" +
                    $"background:{color};box-shadow:0 0 6px {color}99,0 0 2px {color};";
        return new MarkupString($"<span class=\"le-dd-dot\" style=\"{style}\"></span>");
    }

    // ── Formatage texte ───────────────────────────────────────────────────────

    /// <summary>Convertit des secondes en "3d 2h", "45m", "soon", etc.</summary>
    public static string FormatCountdown(int seconds)
    {
        if (seconds <= 0) return "soon";
        int d = seconds / 86400, h = (seconds % 86400) / 3600, m = (seconds % 3600) / 60;
        if (d > 0) return h > 0 ? $"{d}d {h}h" : $"{d}d";
        if (h > 0) return m > 0 ? $"{h}h {m}m" : $"{h}h";
        return $"{m}m";
    }

    /// <summary>Retourne true si la chaîne contient des caractères non-latins (JP, KR, CN, etc.).</summary>
    public static bool ContainsNonLatinScript(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        foreach (char c in s)
        {
            if ((c >= 0x0400 && c <= 0x04FF) || (c >= 0x0600 && c <= 0x06FF) ||
                (c >= 0x0590 && c <= 0x05FF) || (c >= 0x0900 && c <= 0x097F) ||
                (c >= 0x0E00 && c <= 0x0E7F) || (c >= 0x3040 && c <= 0x309F) ||
                (c >= 0x30A0 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF) ||
                (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xAC00 && c <= 0xD7AF))
                return true;
        }
        return false;
    }

    /// <summary>Extrait l'ID vidéo YouTube depuis n'importe quel format d'URL YT.</summary>
    public static string GetYouTubeVideoId(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        try
        {
            var uri   = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (query.AllKeys.Contains("v")) return query["v"] ?? "";
            if (uri.AbsolutePath.Contains("/shorts/"))
            {
                var parts = uri.AbsolutePath.Split("/shorts/", StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 1 ? parts[1].TrimEnd('/') : "";
            }
            if (uri.Segments.Length > 1)
            {
                var last = uri.Segments.LastOrDefault()?.TrimEnd('/');
                if (!string.IsNullOrEmpty(last) && last != "embed") return last;
            }
        }
        catch
        {
            if (url.Contains("watch?v="))  { var p = url.Split("watch?v=");  return p.Length > 1 ? p[1].Split('&')[0] : ""; }
            if (url.Contains("youtu.be/")) { var p = url.Split("youtu.be/"); return p.Length > 1 ? p[1].Split('?')[0] : ""; }
            if (url.Contains("/shorts/"))  { var p = url.Split("/shorts/");  return p.Length > 1 ? p[1].Split('?')[0] : ""; }
        }
        return string.Empty;
    }
}