import 'dart:convert';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:local_auth/local_auth.dart';
import 'package:shared_preferences/shared_preferences.dart';

final biometricServiceProvider = Provider<BiometricService>((ref) => BiometricService());

class BiometricService {
  final LocalAuthentication _auth = LocalAuthentication();

  static const String _keyBiometricEnabled = 'biometric_enabled';
  static const String _keyBiometricUsername = 'biometric_username';
  static const String _keyBiometricPassword = 'biometric_password';

  /// Checks if hardware is available and biometrics are enrolled on device
  Future<bool> isBiometricAvailable() async {
    try {
      final bool canAuthenticateWithBiometrics = await _auth.canCheckBiometrics;
      final bool canAuthenticate = canAuthenticateWithBiometrics || await _auth.isDeviceSupported();
      return canAuthenticate;
    } catch (e) {
      return false;
    }
  }

  /// Checks if user has enabled biometric login in app and credentials exist
  Future<bool> isBiometricLoginEnabled() async {
    final prefs = await SharedPreferences.getInstance();
    final bool isEnabled = prefs.getBool(_keyBiometricEnabled) ?? false;
    final String? username = prefs.getString(_keyBiometricUsername);
    final String? password = prefs.getString(_keyBiometricPassword);
    return isEnabled && username != null && password != null;
  }

  /// Saves user credentials for biometric login
  Future<void> saveBiometricCredentials(String username, String password) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_keyBiometricEnabled, true);
    await prefs.setString(_keyBiometricUsername, username);
    await prefs.setString(_keyBiometricPassword, base64Encode(utf8.encode(password)));
  }

  /// Gets stored credentials
  Future<Map<String, String>?> getBiometricCredentials() async {
    final prefs = await SharedPreferences.getInstance();
    final String? username = prefs.getString(_keyBiometricUsername);
    final String? encodedPassword = prefs.getString(_keyBiometricPassword);
    if (username == null || encodedPassword == null) return null;
    try {
      final password = utf8.decode(base64Decode(encodedPassword));
      return {'username': username, 'password': password};
    } catch (_) {
      return null;
    }
  }

  /// Disables biometric login and removes credentials
  Future<void> disableBiometricLogin() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_keyBiometricEnabled, false);
    await prefs.remove(_keyBiometricUsername);
    await prefs.remove(_keyBiometricPassword);
  }

  /// Authenticate using device biometrics
  Future<bool> authenticate({String localizedReason = 'يرجى تأكيد بصمتك لتسجيل الدخول إلى إقفال'}) async {
    try {
      final bool didAuthenticate = await _auth.authenticate(
        localizedReason: localizedReason,
        biometricOnly: false,
      );
      return didAuthenticate;
    } catch (e) {
      return false;
    }
  }
}
