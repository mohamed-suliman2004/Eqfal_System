import 'package:firebase_core/firebase_core.dart' show FirebaseOptions;
import 'package:flutter/foundation.dart' show defaultTargetPlatform, kIsWeb, TargetPlatform;

class DefaultFirebaseOptions {
  static FirebaseOptions get currentPlatform {
    if (kIsWeb) {
      return web;
    }
    // We only have the web configuration for now
    throw UnsupportedError(
      'DefaultFirebaseOptions have not been configured for this platform - '
      'you can reconfigure this by running the FlutterFire CLI again.',
    );
  }

  static const FirebaseOptions web = FirebaseOptions(
    apiKey: 'AIzaSyA_uBMDaC20p4dOR5S1WYD-CM86wSenLbI',
    appId: '1:100233760473:web:8cb195206ac0bfb94bb8c8',
    messagingSenderId: '100233760473',
    projectId: 'eqfal-system-81268',
    authDomain: 'eqfal-system-81268.firebaseapp.com',
    storageBucket: 'eqfal-system-81268.firebasestorage.app',
    measurementId: 'G-2PG5N3TXQP',
  );
}
