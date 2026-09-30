class AppVersionInfo {
  final String latestVersion;
  final int latestBuildNumber;
  final String minRequiredVersion;
  final int minRequiredBuildNumber;
  final bool isForceUpdate;
  final bool updateAvailable;
  final String title;
  final String releaseNotes;
  final String storeUrl;

  AppVersionInfo({
    required this.latestVersion,
    required this.latestBuildNumber,
    required this.minRequiredVersion,
    required this.minRequiredBuildNumber,
    required this.isForceUpdate,
    required this.updateAvailable,
    required this.title,
    required this.releaseNotes,
    required this.storeUrl,
  });

  factory AppVersionInfo.fromJson(Map<String, dynamic> json) {
    return AppVersionInfo(
      latestVersion: json['latestVersion']?.toString() ?? '1.0.8',
      latestBuildNumber: json['latestBuildNumber'] is int
          ? json['latestBuildNumber']
          : int.tryParse(json['latestBuildNumber']?.toString() ?? '8') ?? 8,
      minRequiredVersion: json['minRequiredVersion']?.toString() ?? '1.0.0',
      minRequiredBuildNumber: json['minRequiredBuildNumber'] is int
          ? json['minRequiredBuildNumber']
          : int.tryParse(json['minRequiredBuildNumber']?.toString() ?? '1') ?? 1,
      isForceUpdate: json['isForceUpdate'] == true,
      updateAvailable: json['updateAvailable'] == true,
      title: json['title']?.toString() ?? 'تحديث جديد متوفر',
      releaseNotes: json['releaseNotes']?.toString() ?? '',
      storeUrl: json['storeUrl']?.toString() ?? 'https://play.google.com/store/apps/details?id=ly.mostanad.eqfal_app',
    );
  }
}
