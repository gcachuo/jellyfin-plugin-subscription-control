# EasyMovie Subscription Plugin for Jellyfin

[![License](https://img.shields.io/github/license/gcachuo/jellyfin-plugin-subscription-control)](LICENSE)
[![Release](https://img.shields.io/github/v/release/gcachuo/jellyfin-plugin-subscription-control)](https://github.com/gcachuo/jellyfin-plugin-subscription-control/releases)

Integrates EasyMovie subscription status with Jellyfin playback. Controls media access based on user subscription status and displays subscription-specific preroll videos before content.

## Features

- ✨ **Subscription Status Integration** - Connects to EasyMovie API to check user subscription status
- 🎬 **Dynamic Preroll Videos** - Displays different preroll videos based on subscription state (active, expiring, courtesy)
- 🚫 **Content Blocking** - Prevents expired users from accessing content and shows expiration message
- 🎁 **Trial User Detection** - Automatically detects and handles trial subscriptions
- 🛡️ **Fail-Safe Mode** - Defaults to active status if API is unavailable (ensures uninterrupted service)
- 💾 **Smart Caching** - Reduces API calls with configurable cache duration

## Requirements

- Jellyfin 10.11.6 or higher
- .NET 9.0 runtime
- EasyMovie subscription API endpoint

## Installation

### Method 1: From Repository (Recommended)

1. Open Jellyfin Dashboard
2. Navigate to **Plugins → Repositories**
3. Add repository URL:
   ```
   https://raw.githubusercontent.com/gcachuo/jellyfin-plugin-subscription-control/main/manifest.json
   ```
4. Go to **Plugins → Catalog**
5. Find **EasyMovie Subscription** and click **Install**
6. Restart Jellyfin

### Method 2: Manual Installation

1. Download the latest release from [Releases](https://github.com/gcachuo/jellyfin-plugin-subscription-control/releases)
2. Extract the ZIP file
3. Copy the contents to your Jellyfin plugins directory:
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\EasyMovie_1.0.0.0\`
   - Linux: `/var/lib/jellyfin/plugins/EasyMovie_1.0.0.0/`
   - Docker: `/config/plugins/EasyMovie_1.0.0.0/`
4. Restart Jellyfin

## Configuration

1. Navigate to **Dashboard → Plugins → EasyMovie Subscription**
2. Configure the following settings:

### API Settings
- **API URL**: Your EasyMovie subscription API endpoint
  - Example: `https://easymovie.lat/api/subscription.php`
- **Cache Duration (minutes)**: How long to cache subscription status (default: 10)

### Subscription Thresholds
- **Expiring Threshold (days)**: Days before expiration to show "expiring" status (default: 7)
- **Trial Max Duration (days)**: Maximum days to consider a subscription as trial (default: 14)

### Preroll Video Paths
Configure absolute paths to your preroll videos:
- **Active Video**: Shown to users with active subscriptions
- **Expiring Video**: Shown to users whose subscription is expiring soon
- **Expired Video**: Shown to users with expired subscriptions (replaces all content)
- **Courtesy Video**: Shown to users with courtesy access

**Important:** Videos must be:
- Stored on the Jellyfin server filesystem
- Indexed in a Jellyfin library for best client compatibility
- Accessible by the Jellyfin process

## Subscription States

| State | Description | Behavior |
|-------|-------------|----------|
| **Active** | Valid subscription, not expiring soon | Shows active preroll, full access |
| **Expiring** | Subscription expires within threshold days | Shows expiring preroll, full access |
| **Expired** | Subscription has expired | Blocks content, shows expired video |
| **Courtesy** | Manual courtesy access granted | Shows courtesy preroll, full access |
| **Trial** | New subscription within trial period | Shows expiring preroll, full access |

## API Integration

The plugin expects the following JSON response from your API:

```json
{
  "status": "active",
  "expirationDate": "2026-08-01T15:12:23.000-04:00",
  "daysUntilExpiration": 24,
  "email": "user@example.com",
  "isTrial": false,
  "subscriptionDuration": 30,
  "failsafe": false
}
```

### API Parameters
The plugin sends these query parameters:
- `userId`: Jellyfin user ID (GUID)
- `expiringDays`: Configured expiring threshold
- `trialMaxDays`: Configured trial max duration
- `cacheMinutes`: Configured cache duration

## Troubleshooting

### Plugin not showing configuration page
- Ensure the plugin is enabled in **Dashboard → Plugins**
- Restart Jellyfin after installation

### Preroll videos not playing
- Verify video paths are absolute and accessible
- Check that videos are indexed in a Jellyfin library
- Review Jellyfin logs for errors

### API connection issues
- Test API endpoint manually with curl:
  ```bash
  curl "https://your-api.com/subscription.php?userId=test@example.com"
  ```
- Check Jellyfin logs for API errors
- Verify fail-safe mode is working (users should still have access)

### Content blocking not working for expired users
- Ensure "Expired Video" path is configured
- Check that PlaybackInterceptor service is running
- Review Jellyfin logs for playback interception errors

## Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/gcachuo/jellyfin-plugin-subscription-control.git
cd jellyfin-plugin-subscription-control

# Build plugin
dotnet build EasyMovie.Plugin/EasyMovie.Plugin.csproj -c Release

# Output will be in:
# EasyMovie.Plugin/bin/Release/net9.0/
```

### Creating a Release Package

**Windows (PowerShell):**
```powershell
.\package.ps1
```

**Linux/WSL:**
```bash
chmod +x package.sh
./package.sh
```

This will create `EasyMovie.Plugin-1.0.0.0.zip` and display the MD5 checksum for the manifest.

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/gcachuo/jellyfin-plugin-subscription-control/issues)
- **Discussions**: [GitHub Discussions](https://github.com/gcachuo/jellyfin-plugin-subscription-control/discussions)

## Acknowledgments

- Built for [Jellyfin](https://jellyfin.org/)
- Inspired by [Local Intros Plugin](https://github.com/jellyfin/jellyfin-plugin-intros)
- Part of the [EasyMovie](https://easymovie.lat) ecosystem
