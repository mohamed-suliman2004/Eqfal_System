import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../providers/operations_provider.dart';
import '../services/signalr_service.dart';
import '../models/country_code.dart';
import '../widgets/country_phone_input.dart';
import '../services/app_version_service.dart';
import '../services/biometric_service.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  // 0: Phone, 1: Email
  int _selectedTab = 0;

  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  CountryCode _selectedCountry = CountryCode.defaultCountry; // +218 Libya
  bool _isLoading = false;
  bool _obscurePassword = true;
  bool _isBiometricAvailable = false;
  bool _isBiometricEnabled = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _phoneController.addListener(_clearError);
    _emailController.addListener(_clearError);
    _passwordController.addListener(_clearError);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        AppVersionService.checkVersion(context);
        _checkBiometrics();
      }
    });
  }

  void _clearError() {
    if (_errorMessage != null) {
      setState(() {
        _errorMessage = null;
      });
    }
  }

  Future<void> _checkBiometrics() async {
    final bioService = ref.read(biometricServiceProvider);
    final available = await bioService.isBiometricAvailable();
    final enabled = await bioService.isBiometricLoginEnabled();
    if (mounted) {
      setState(() {
        _isBiometricAvailable = available;
        _isBiometricEnabled = enabled;
      });
      if (enabled) {
        _loginWithBiometrics();
      }
    }
  }

  Future<void> _loginWithBiometrics() async {
    if (_isLoading) return;
    final bioService = ref.read(biometricServiceProvider);
    final creds = await bioService.getBiometricCredentials();
    if (creds == null) return;

    final authenticated = await bioService.authenticate();
    if (!authenticated || !mounted) return;

    setState(() {
      _isLoading = true;
    });

    try {
      final apiService = ref.read(apiServiceProvider);
      final success = await apiService.login(creds['username']!, creds['password']!);

      if (!mounted) return;

      if (success) {
        ref.invalidate(operationsProvider);
        ref.read(signalRServiceProvider).initializeConnection();

        final prefs = await SharedPreferences.getInstance();
        final fcmToken = prefs.getString('fcm_token');
        if (fcmToken != null) {
          try {
            await apiService.updateFcmToken(fcmToken);
          } catch (_) {}
        }

        context.go('/');
      } else {
        _showErrorMessage('فشل تسجيل الدخول بالبصمة، يرجى كتابة البيانات يدويًا');
      }
    } catch (e) {
      if (!mounted) return;
      _showErrorMessage('فشل تسجيل الدخول بالبصمة، يرجى كتابة البيانات يدويًا');
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _showErrorMessage(String message) {
    if (!mounted) return;
    final cleanMsg = message.replaceAll('\u0620', ' ').trim();
    setState(() {
      _errorMessage = cleanMsg;
    });

    ScaffoldMessenger.of(context).clearSnackBars();
  }

  @override
  void dispose() {
    _phoneController.removeListener(_clearError);
    _emailController.removeListener(_clearError);
    _passwordController.removeListener(_clearError);
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _login() async {
    setState(() {
      _errorMessage = null;
    });
    String username;

    if (_selectedTab == 0) {
      final rawPhone = _phoneController.text.trim();
      if (rawPhone.isEmpty) {
        _showErrorMessage('يرجى إدخال رقم الهاتف');
        return;
      }
      // Remove leading zero if entered with dial code (e.g. 091 -> 91 with +218)
      String cleanPhone = rawPhone.replaceAll(RegExp(r'^[0]+'), '');
      username = '${_selectedCountry.dialCode}$cleanPhone';
    } else {
      username = _emailController.text.trim();
      if (username.isEmpty) {
        _showErrorMessage('يرجى إدخال البريد الإلكتروني أو اسم المستخدم');
        return;
      }
    }

    final password = _passwordController.text;
    if (password.isEmpty) {
      _showErrorMessage('يرجى إدخال كلمة المرور');
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      final apiService = ref.read(apiServiceProvider);
      final success = await apiService.login(username, password);

      if (!mounted) return;

      if (success) {
        ref.invalidate(operationsProvider);
        ref.read(signalRServiceProvider).initializeConnection();

        final prefs = await SharedPreferences.getInstance();
        final fcmToken = prefs.getString('fcm_token');
        if (fcmToken != null) {
          try {
            await apiService.updateFcmToken(fcmToken);
          } catch (_) {}
        }

        final bioService = ref.read(biometricServiceProvider);
        if (_isBiometricAvailable && !_isBiometricEnabled) {
          final shouldEnable = await showDialog<bool>(
            context: context,
            barrierDismissible: false,
            builder: (ctx) => AlertDialog(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
              title: Row(
                children: const [
                  Icon(Icons.fingerprint, color: Color(0xFF2563EB), size: 28),
                  SizedBox(width: 8),
                  Text('تسجيل الدخول بالبصمة', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                ],
              ),
              content: const Text(
                'هل ترغب في تفعيل تسجيل الدخول بالبصمة لتسجيل الدخول السريع لاحقاً وتجنب نسيان كلمة المرور؟',
                style: TextStyle(fontSize: 14, color: Color(0xFF475569)),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(ctx, false),
                  child: const Text('ليس الآن', style: TextStyle(color: Color(0xFF64748B))),
                ),
                ElevatedButton(
                  onPressed: () => Navigator.pop(ctx, true),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF2563EB),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                  child: const Text('تفعيل البصمة', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                ),
              ],
            ),
          );

          if (shouldEnable == true) {
            await bioService.saveBiometricCredentials(username, password);
          }
        } else if (_isBiometricEnabled) {
          await bioService.saveBiometricCredentials(username, password);
        }

        if (!mounted) return;
        context.go('/');

      } else {
        _showErrorMessage('بيانات الدخول غير صحيحة');
      }
    } catch (e) {
      if (!mounted) return;
      String errorMsg = 'بيانات الدخول غير صحيحة';
      final rawError = e.toString().replaceAll('Exception: ', '').trim();
      if (rawError.isNotEmpty && !rawError.contains('XMLHttpRequest') && !rawError.contains('DioException')) {
        errorMsg = rawError;
      }
      _showErrorMessage(errorMsg);
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 32.0),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  _buildLogo(),
                  const SizedBox(height: 32),
                  const Text(
                    'مرحباً بعودتك',
                    style: TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.w900,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'سجّل دخولك للمتابعة إلى لوحة إقفال',
                    style: TextStyle(
                      fontSize: 14,
                      color: Color(0xFF64748B),
                    ),
                  ),
                  const SizedBox(height: 32),
                  Container(
                    padding: const EdgeInsets.all(28.0),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(24),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.04),
                          blurRadius: 20,
                          offset: const Offset(0, 8),
                        ),
                      ],
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Login Mode Switcher (Phone / Email)
                        Container(
                          padding: const EdgeInsets.all(4),
                          decoration: BoxDecoration(
                            color: const Color(0xFFF1F5F9),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Row(
                            children: [
                              Expanded(
                                child: InkWell(
                                  onTap: () => setState(() {
                                    _selectedTab = 0;
                                    _errorMessage = null;
                                  }),
                                  borderRadius: BorderRadius.circular(10),
                                  child: Container(
                                    padding: const EdgeInsets.symmetric(vertical: 10),
                                    decoration: BoxDecoration(
                                      color: _selectedTab == 0 ? Colors.white : Colors.transparent,
                                      borderRadius: BorderRadius.circular(10),
                                      boxShadow: _selectedTab == 0
                                          ? [
                                              BoxShadow(
                                                color: Colors.black.withValues(alpha: 0.05),
                                                blurRadius: 4,
                                                offset: const Offset(0, 1),
                                              )
                                            ]
                                          : null,
                                    ),
                                    child: Row(
                                      mainAxisAlignment: MainAxisAlignment.center,
                                      children: [
                                        Icon(
                                          Icons.phone_android_rounded,
                                          size: 16,
                                          color: _selectedTab == 0 ? const Color(0xFF2563EB) : const Color(0xFF64748B),
                                        ),
                                        const SizedBox(width: 6),
                                        Text(
                                          'رقم الهاتف',
                                          style: TextStyle(
                                            fontSize: 13,
                                            fontWeight: _selectedTab == 0 ? FontWeight.bold : FontWeight.normal,
                                            color: _selectedTab == 0 ? const Color(0xFF2563EB) : const Color(0xFF64748B),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                              ),
                              Expanded(
                                child: InkWell(
                                  onTap: () => setState(() {
                                    _selectedTab = 1;
                                    _errorMessage = null;
                                  }),
                                  borderRadius: BorderRadius.circular(10),
                                  child: Container(
                                    padding: const EdgeInsets.symmetric(vertical: 10),
                                    decoration: BoxDecoration(
                                      color: _selectedTab == 1 ? Colors.white : Colors.transparent,
                                      borderRadius: BorderRadius.circular(10),
                                      boxShadow: _selectedTab == 1
                                          ? [
                                              BoxShadow(
                                                color: Colors.black.withValues(alpha: 0.05),
                                                blurRadius: 4,
                                                offset: const Offset(0, 1),
                                              )
                                            ]
                                          : null,
                                    ),
                                    child: Row(
                                      mainAxisAlignment: MainAxisAlignment.center,
                                      children: [
                                        Icon(
                                          Icons.email_outlined,
                                          size: 16,
                                          color: _selectedTab == 1 ? const Color(0xFF2563EB) : const Color(0xFF64748B),
                                        ),
                                        const SizedBox(width: 6),
                                        Text(
                                          'البريد الإلكتروني',
                                          style: TextStyle(
                                            fontSize: 13,
                                            fontWeight: _selectedTab == 1 ? FontWeight.bold : FontWeight.normal,
                                            color: _selectedTab == 1 ? const Color(0xFF2563EB) : const Color(0xFF64748B),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 20),

                        // Input Field depending on Tab
                        if (_selectedTab == 0) ...[
                          const Text(
                            'رقم الهاتف مع رمز الدولة',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF334155),
                            ),
                          ),
                          const SizedBox(height: 8),
                          CountryPhoneInput(
                            controller: _phoneController,
                            initialCountry: _selectedCountry,
                            onCountryChanged: (c) => setState(() => _selectedCountry = c),
                          ),
                        ] else ...[
                          const Text(
                            'البريد الإلكتروني أو اسم المستخدم',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF334155),
                            ),
                          ),
                          const SizedBox(height: 8),
                          _buildTextField(
                            controller: _emailController,
                            hint: 'admin@eqfal.com',
                            prefixIcon: Icons.email_outlined,
                            isLtr: true,
                          ),
                        ],

                        const SizedBox(height: 20),
                        const Text(
                          'كلمة المرور',
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF334155),
                          ),
                        ),
                        const SizedBox(height: 8),
                        _buildTextField(
                          controller: _passwordController,
                          hint: '••••••••',
                          prefixIcon: Icons.lock_outline,
                          isPassword: true,
                          isLtr: true,
                        ),
                        const SizedBox(height: 14),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            onPressed: () {
                              context.push('/forgot-password');
                            },
                            style: TextButton.styleFrom(
                              padding: EdgeInsets.zero,
                              minimumSize: const Size(0, 0),
                              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                            ),
                            child: const Text(
                              'نسيت كلمة المرور؟',
                              style: TextStyle(
                                color: Color(0xFF3B82F6),
                                fontWeight: FontWeight.w600,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ),
                        if (_errorMessage != null) ...[
                          const SizedBox(height: 16),
                          Container(
                            width: double.infinity,
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFFEF2F2),
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: const Color(0xFFFECACA)),
                            ),
                            child: Row(
                              children: [
                                const Icon(
                                  Icons.error_outline_rounded,
                                  color: Color(0xFFDC2626),
                                  size: 20,
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Text(
                                    _errorMessage!,
                                    style: const TextStyle(
                                      color: Color(0xFF991B1B),
                                      fontSize: 13,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                                InkWell(
                                  onTap: () => setState(() => _errorMessage = null),
                                  borderRadius: BorderRadius.circular(12),
                                  child: const Padding(
                                    padding: EdgeInsets.all(4.0),
                                    child: Icon(
                                      Icons.close,
                                      color: Color(0xFF991B1B),
                                      size: 16,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                        const SizedBox(height: 20),
                        SizedBox(
                          width: double.infinity,
                          height: 50,
                          child: ElevatedButton(
                            onPressed: _isLoading ? null : _login,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFF2563EB),
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(12),
                              ),
                              elevation: 0,
                            ),
                            child: _isLoading
                                ? const SizedBox(
                                    width: 22,
                                    height: 22,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2.5,
                                      valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                                    ),
                                  )
                                : const Text(
                                    'تسجيل الدخول',
                                    style: TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                          ),
                        ),
                        if (_isBiometricEnabled) ...[
                          const SizedBox(height: 12),
                          SizedBox(
                            width: double.infinity,
                            height: 50,
                            child: OutlinedButton.icon(
                              onPressed: _isLoading ? null : _loginWithBiometrics,
                              icon: const Icon(Icons.fingerprint, color: Color(0xFF2563EB), size: 24),
                              label: const Text(
                                'تسجيل الدخول بالبصمة',
                                style: TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.bold,
                                  color: Color(0xFF2563EB),
                                ),
                              ),
                              style: OutlinedButton.styleFrom(
                                side: const BorderSide(color: Color(0xFF2563EB), width: 1.5),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(12),
                                ),
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 28),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Text(
                        'ليس لديك حساب؟',
                        style: TextStyle(color: Color(0xFF64748B), fontSize: 14),
                      ),
                      TextButton(
                        onPressed: () => context.go('/register'),
                        child: const Text(
                          'إنشاء حساب',
                          style: TextStyle(
                            color: Color(0xFF2563EB),
                            fontWeight: FontWeight.bold,
                            fontSize: 14,
                          ),
                        ),
                      ),
                    ],
                  )
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLogo() {
    return Image.asset(
      'assets/images/eqfal-logo.png',
      height: 70,
      errorBuilder: (context, error, stackTrace) {
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    _dot(), _dot(), const SizedBox(width: 4), _line(20),
                  ],
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    _dot(), const SizedBox(width: 4), _line(25),
                  ],
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    _dot(), _dot(), const SizedBox(width: 4), _line(15),
                  ],
                ),
              ],
            ),
            const SizedBox(width: 8),
            Container(width: 6, height: 40, decoration: BoxDecoration(color: const Color(0xFF2563EB), borderRadius: BorderRadius.circular(4))),
            const SizedBox(width: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: const [
                Text('إقفال', style: TextStyle(fontSize: 36, fontWeight: FontWeight.w900, color: Color(0xFF1E293B), height: 1)),
                SizedBox(height: 4),
                Text('حوّل رسائلك إلى عمليات', style: TextStyle(fontSize: 11, color: Color(0xFF64748B), fontWeight: FontWeight.bold)),
              ],
            )
          ],
        );
      },
    );
  }
  
  Widget _dot() => Container(margin: const EdgeInsets.only(right: 4), width: 6, height: 6, decoration: const BoxDecoration(color: Color(0xFF94A3B8), shape: BoxShape.circle));
  Widget _line(double w) => Container(width: w, height: 6, decoration: BoxDecoration(color: const Color(0xFF2563EB), borderRadius: BorderRadius.circular(4)));

  Widget _buildTextField({
    required TextEditingController controller,
    required String hint,
    required IconData prefixIcon,
    bool isPassword = false,
    bool isLtr = false,
  }) {
    return TextFormField(
      controller: controller,
      obscureText: isPassword && _obscurePassword,
      textDirection: isLtr ? TextDirection.ltr : TextDirection.rtl,
      textAlign: isLtr ? TextAlign.left : TextAlign.right,
      decoration: InputDecoration(
        hintText: hint,
        hintTextDirection: isLtr ? TextDirection.ltr : TextDirection.rtl,
        hintStyle: const TextStyle(color: Color(0xFFCBD5E1), fontSize: 13),
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: Colors.grey.shade300),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: Colors.grey.shade300),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFF2563EB), width: 1.5),
        ),
        prefixIcon: Icon(prefixIcon, color: const Color(0xFF94A3B8), size: 20),
        suffixIcon: isPassword
            ? IconButton(
                icon: Icon(
                  _obscurePassword ? Icons.visibility_outlined : Icons.visibility_off_outlined,
                  color: const Color(0xFF94A3B8),
                  size: 20,
                ),
                onPressed: () {
                  setState(() {
                    _obscurePassword = !_obscurePassword;
                  });
                },
              )
            : null,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      ),
    );
  }
}