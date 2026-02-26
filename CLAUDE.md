# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build WebAssembly client
dotnet build AniScroll.Web/AniScroll.Web.csproj

# Run development server
dotnet run --project AniScroll.Web/AniScroll.Web.csproj

# Build MAUI app (requires .NET MAUI workload)
dotnet build AniScroll.Maui/AniScroll.Maui.csproj

# Publish for Netlify deployment
dotnet publish -c Release -o bin/Release/net8.0/publish AniScroll.Web/AniScroll.Web.csproj
```

## Architecture

### Project Structure
- **AniScroll.Web** - Blazor WebAssembly client (net8.0), deployed on Netlify
- **AniScroll.Maui** - .NET MAUI app for iOS/Android/Windows (net10.0)
- **AniScroll.Shared** - Shared Razor components, models and services (net8.0)

### Data Flow
- **AniListService** (`AniScroll.Shared/Services/`) - GraphQL client for AniList API with rate limiting (30 req/min), fallback to Jikan API for search
- **AnimeCard** - Main model with extended fields (tags, relations, external links, rankings)
- **Index.razor** - Main scrollable card interface with virtualized rendering
- **AnimeDetailPopup.razor** - Modal with full anime details including spoiler tags for relations

### Key Features
- Touch/mouse drag gesture for card navigation
- Image preloading for smooth scrolling
- Search via Jikan API with relevance scoring
- Rate limit handling with automatic retry
- Spoiler tags hidden behind click-to-reveal button

### Styling
- Single CSS file (`AniScroll.Shared/wwwroot/css/app.css`)
- Dark theme optimized for anime artwork
- Responsive design for mobile/desktop
