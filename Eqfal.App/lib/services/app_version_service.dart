import 'dart:io' show Platform;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
import '../config.dart';
import '../models/app_version_info.dart';
import '../widgets/app_update_dialog.dart';

class AppVersionService {
  static bool _hasCheckedThisSession = false;

  /// Checks if a new version is available on the server.
  /// [isManual] is true when triggered manually from Settings screen.
  static Future<void> checkVersion(BuildContext context, {bool isManual = false}) async {
    if (!isManual && _hasCheckedThisSession) {
      return;
    }

    try {
      String platform = 'android';
      if (kIsWeb) {
        platform = 'web';
      } else if (!kIsWeb && Platform.isIOS) {
        platform = 'ios';
      }

      final dio = Dio(BaseOptions(
        baseUrl: Config.apiUrl,
        connectTimeout: const Duration(seconds: 5),
        receiveTimeout: const Duration(seconds: 5),
      ));

      final response = await dio.get('/app-version', queryParameters: {
        'platform': platform,
        'buildNumber': Config.currentBuildNumber,
      });

      if (response.statusCode == 200 && response.data != null) {
        final data = response.data is Map<String, dynamic>
            ? response.data as Map<String, dynamic>
            : Map<String, dynamic>.from(response.data);

        final versionInfo = AppVersionInfo.fromJson(data);

        final bool hasNewBuild = versionInfo.latestBuildNumber > Config.currentBuildNumber;
        final bool isForced = versionInfo.isForceUpdate || (Config.currentBuildNumber < versionInfo.minRequiredBuildNumber);

        if (hasNewBuild || versionInfo.updateAvailable) {
          if (!context.mounted) return;

          _hasCheckedThisSession = true;

          if (context.mounted) {
            await showDialog(
              context: context,
              barrierDismissible: !isForced,
              builder: (dialogContext) => PopScope(
                canPop: !isForced,
                child: AppUpdateDialog(
                  versionInfo: versionInfo,
                  isForceUpdate: isForced,
                ),
              ),
            );
          }
        } else {
          _hasCheckedThisSession = true;
          if (isManual && context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Row(
                  children: [
                    const Icon(Icons.check_circle_outline, color: Colors.white),
                    const SizedBox(width: 8),
                    Text(
                      'تطبيقك محدث إلى آخر إصدار (${Config.currentVersion})',
                      style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                backgroundColor: const Color(0xFF10B981),
                behavior: SnackBarBehavior.floating,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            );
          }
        }
      }
    } catch (e) {
      debugPrint('AppVersionService checkVersion error: $e');
      if (isManual && context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: const Text(
              'تعذر التحقق من التحديثات حالياً، يرجى المحاولة لاحقاً.',
              style: TextStyle(fontFamily: 'Cairo'),
            ),
            backgroundColor: Colors.grey.shade700,
            behavior: SnackBarBehavior.floating,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          ),
        );
      }
    }
  }
}
