import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'firebase_options.dart';

import 'package:shared_preferences/shared_preferences.dart';
import 'screens/dashboard_screen.dart';
import 'screens/operation_details_screen.dart';
import 'screens/edit_operation_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/forgot_password_screen.dart';
import 'screens/settings_screen.dart';
import 'screens/operations_list_screen.dart';
import 'screens/drafts_screen.dart';
import 'screens/dictionaries_screen.dart';
import 'screens/keywords_screen.dart';
import 'screens/currencies_screen.dart';
import 'screens/analyze_message_screen.dart';
import 'screens/monitored_numbers_screen.dart';
import 'screens/onboarding_screen.dart';
import 'screens/subscription_plans_screen.dart';
import 'screens/checkout_screen.dart';
import 'models/subscription_models.dart';
import 'services/signalr_service.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  
  try {
    await Firebase.initializeApp(
      options: DefaultFirebaseOptions.currentPlatform,
    );

    FirebaseMessaging messaging = FirebaseMessaging.instance;
    NotificationSettings settings = await messaging.requestPermission(
      alert: true,
      announcement: false,
      badge: true,
      carPlay: false,
      criticalAlert: false,
      provisional: false,
      sound: true,
    );

    if (settings.authorizationStatus == AuthorizationStatus.authorized) {
      String? token = await messaging.getToken(
        vapidKey: 'BNHGNQrlUruAxqWwwZ8xFvz56MXvgKrzcjNA7aLRYyPnPElQO06faw_jmC_QNGhAEYdaWRma6bPMjmh8pTlX18c',
      );
      if (token != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('fcm_token', token);
        print('FCM Token: $token');
      }
    }
  } catch (e) {
    print('Firebase Initialization or Messaging Error: $e');
  }

  final prefs = await SharedPreferences.getInstance();
  final String initialRoute = (prefs.getString('jwt_token') != null) ? '/' : '/login';

  runApp(
    ProviderScope(
      child: MyApp(initialRoute: initialRoute),
    ),
  );
}

GoRouter _createRouter(String initialRoute) {
  return GoRouter(
    initialLocation: initialRoute,
    redirect: (context, state) async {
      final prefs = await SharedPreferences.getInstance();
      final token = prefs.getString('jwt_token');
      
      final hasSeenOnboarding = prefs.getBool('has_seen_onboarding') ?? false;
      
      final isLoggingIn = state.uri.path == '/login';
      final isRegistering = state.uri.path == '/register';
      final isForgotPassword = state.uri.path == '/forgot-password';
      final isOnboarding = state.uri.path == '/onboarding';
      
      // If no token and not already on login or register, force to login
      if (token == null && !isLoggingIn && !isRegistering && !isForgotPassword) {
        return '/login';
      }
      
      // If token exists and trying to access auth pages
      if (token != null && (isLoggingIn || isRegistering || isForgotPassword)) {
        if (!hasSeenOnboarding) {
          return '/onboarding';
        }
        return '/';
      }
      
      // If logged in and hasn't seen onboarding yet
      if (token != null && !hasSeenOnboarding && !isOnboarding) {
        return '/onboarding';
      }
      
      // Otherwise, let them go to their intended route
      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/register',
        builder: (context, state) => const RegisterScreen(),
      ),
      GoRoute(
        path: '/forgot-password',
        builder: (context, state) => const ForgotPasswordScreen(),
      ),
      GoRoute(
        path: '/onboarding',
        builder: (context, state) {
          final fromSettings = state.uri.queryParameters['fromSettings'] == 'true';
          return OnboardingScreen(isFromSettings: fromSettings);
        },
      ),
    GoRoute(
      path: '/',
      builder: (context, state) => const DashboardScreen(),
    ),
    GoRoute(
      path: '/operations/:id',
      builder: (context, state) => OperationDetailsScreen(id: state.pathParameters['id']!),
    ),
    GoRoute(
      path: '/operations/:id/edit',
      builder: (context, state) => EditOperationScreen(id: state.pathParameters['id']!),
    ),
    GoRoute(
      path: '/edit/:id',
      builder: (context, state) => EditOperationScreen(id: state.pathParameters['id']!),
    ),
    GoRoute(
      path: '/list/:category',
      builder: (context, state) => OperationsListScreen(category: state.pathParameters['category']!),
    ),
    GoRoute(
      path: '/drafts',
      builder: (context, state) => const DraftsScreen(),
    ),
    GoRoute(
      path: '/settings',
      builder: (context, state) => const SettingsScreen(),
    ),
    GoRoute(
      path: '/dictionaries',
      builder: (context, state) => const DictionariesScreen(),
    ),
    GoRoute(
      path: '/currencies',
      builder: (context, state) => const CurrenciesScreen(),
    ),
    GoRoute(
      path: '/keywords/:category',
      builder: (context, state) => KeywordsScreen(category: state.pathParameters['category']!),
    ),
    GoRoute(
      path: '/analyze',
      builder: (context, state) => const AnalyzeMessageScreen(),
    ),
    GoRoute(
      path: '/monitored-numbers',
      builder: (context, state) => const MonitoredNumbersScreen(),
    ),
    GoRoute(
      path: '/subscriptions',
      builder: (context, state) => const SubscriptionPlansScreen(),
    ),
    GoRoute(
      path: '/checkout',
      builder: (context, state) {
        final extra = state.extra as Map<String, dynamic>;
        return CheckoutScreen(
          plan: extra['plan'] as SubscriptionPlanItem,
          price: extra['price'] as PlanPriceItem,
          cycle: extra['cycle'] as int,
          confirmReplace: extra['confirmReplace'] as bool? ?? false,
        );
      },
    ),
  ],
);
}

class MyApp extends ConsumerStatefulWidget {
  final String initialRoute;
  const MyApp({super.key, required this.initialRoute});

  @override
  ConsumerState<MyApp> createState() => _MyAppState();
}

class _MyAppState extends ConsumerState<MyApp> {
  late final GoRouter _router;

  @override
  void initState() {
    super.initState();
    _router = _createRouter(widget.initialRoute);
    // Initialize SignalR Connection
    Future.microtask(() {
      ref.read(signalRServiceProvider).initializeConnection();
    });
  }

  @override
  void dispose() {
    ref.read(signalRServiceProvider).stopConnection();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Eqfal Operations Hub',
      debugShowCheckedModeBanner: false,
      routerConfig: _router,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
        useMaterial3: true,
        textTheme: GoogleFonts.cairoTextTheme(Theme.of(context).textTheme),
        snackBarTheme: SnackBarThemeData(
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          elevation: 6,
        ),
      ),
      // RTL Support for Arabic
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [
        Locale('ar', 'AE'), // Arabic
      ],
      locale: const Locale('ar', 'AE'),
    );
  }
}
