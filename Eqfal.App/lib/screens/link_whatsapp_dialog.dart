import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../providers/operations_provider.dart';
import '../models/country_code.dart';
import '../widgets/country_phone_input.dart';
import '../widgets/page_help_dialog.dart';

class LinkWhatsAppDialog extends ConsumerStatefulWidget {
  final VoidCallback? onLinked;
  const LinkWhatsAppDialog({super.key, this.onLinked});

  @override
  ConsumerState<LinkWhatsAppDialog> createState() => _LinkWhatsAppDialogState();
}

class _LinkWhatsAppDialogState extends ConsumerState<LinkWhatsAppDialog> {
  final TextEditingController _phoneController = TextEditingController();
  CountryCode _selectedCountry = CountryCode.defaultCountry;
  final _formKey = GlobalKey<FormState>();
  
  bool _isLoading = false;
  String? _loadingMessage;
  String? _pairingCode;
  String? _errorMessage;
  bool _isCopied = false;
  
  int _secondsRemaining = 120;
  Timer? _countdownTimer;
  Timer? _statusCheckTimer;
  bool _isLinkedSuccessfully = false;

  @override
  void dispose() {
    _countdownTimer?.cancel();
    _statusCheckTimer?.cancel();
    _phoneController.dispose();
    super.dispose();
  }

  String _getFullPhoneNumber() {
    final localPhone = _phoneController.text.trim().replaceAll(RegExp(r'^[0]+'), '');
    return '${_selectedCountry.code}$localPhone';
  }

  Future<void> _requestPairingCode({bool forceReset = false}) async {
    if (!_formKey.currentState!.validate()) return;

    _countdownTimer?.cancel();
    _statusCheckTimer?.cancel();

    setState(() {
      _isLoading = true;
      _loadingMessage = forceReset 
          ? 'جاري تنظيف الجلسة وتهيئة اتصال جديد مع واتساب...'
          : 'جاري إنشاء اتصال آمن وتوليد رمز الربط...';
      _errorMessage = null;
      _pairingCode = null;
      _isCopied = false;
      _isLinkedSuccessfully = false;
      _secondsRemaining = 120;
    });

    try {
      final fullPhone = _getFullPhoneNumber();
      final api = ref.read(apiServiceProvider);
      final res = await api.requestWhatsAppPairingCode(phone: fullPhone, forceReset: forceReset);

      if (!mounted) return;

      if (res['alreadyConnected'] == true) {
        _handleSuccessfulLink(message: 'حساب واتساب متصل بالفعل!');
        return;
      }

      if (res['success'] == true && res['code'] != null) {
        final rawCode = res['code'].toString().trim().toUpperCase().replaceAll('-', '');
        final bool isValid = rawCode.length == 8 &&
            rawCode != 'RESPONSE' &&
            rawCode.contains(RegExp(r'\d'));

        if (isValid) {
          setState(() {
            _pairingCode = res['code'];
            _secondsRemaining = (res['expiresInSeconds'] is int) ? res['expiresInSeconds'] : 120;
            _isLoading = false;
            _loadingMessage = null;
          });

          _startCountdown();
          _startLiveStatusCheck();
          return;
        }
      }

      setState(() {
        _errorMessage = res['message'] ?? 'تعذر توليد رمز الربط من خادم واتساب، يرجى المحاولة بعد قليل.';
        _isLoading = false;
        _loadingMessage = null;
      });
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = 'حدث خطأ في الاتصال بالخادم: $e';
          _isLoading = false;
          _loadingMessage = null;
        });
      }
    }
  }

  void _startCountdown() {
    _countdownTimer?.cancel();
    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        if (_secondsRemaining > 0) {
          _secondsRemaining--;
        } else {
          timer.cancel();
        }
      });
    });
  }

  void _startLiveStatusCheck() {
    _statusCheckTimer?.cancel();
    _statusCheckTimer = Timer.periodic(const Duration(seconds: 3), (timer) async {
      if (!mounted || _isLinkedSuccessfully) {
        timer.cancel();
        return;
      }
      try {
        final statusRes = await ref.read(apiServiceProvider).getWhatsAppStatus();
        if (statusRes['connected'] == true || statusRes['status'] == 'متصل') {
          timer.cancel();
          _handleSuccessfulLink();
        }
      } catch (_) {}
    });
  }

  void _handleSuccessfulLink({String? message}) {
    _countdownTimer?.cancel();
    _statusCheckTimer?.cancel();
    HapticFeedback.mediumImpact();

    if (mounted) {
      setState(() {
        _isLinkedSuccessfully = true;
        _pairingCode = null;
        _isLoading = false;
      });
    }

    Future.delayed(const Duration(milliseconds: 1400), () {
      if (mounted) {
        Navigator.of(context).pop();
        widget.onLinked?.call();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Row(
              children: [
                const Icon(Icons.check_circle_outline, color: Colors.white),
                const SizedBox(width: 8),
                Text(message ?? 'تم ربط رقم الواتساب بنجاح! 🎉'),
              ],
            ),
            backgroundColor: const Color(0xFF10B981),
            behavior: SnackBarBehavior.floating,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          ),
        );
      }
    });
  }

  void _copyToClipboard() {
    if (_pairingCode != null) {
      Clipboard.setData(ClipboardData(text: _pairingCode!));
      HapticFeedback.lightImpact();
      setState(() {
        _isCopied = true;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تم نسخ رمز الربط بنجاح! 📋'),
          duration: Duration(seconds: 2),
          backgroundColor: Color(0xFF25D366),
        ),
      );
    }
  }

  String _formatTimer(int seconds) {
    final m = seconds ~/ 60;
    final s = seconds % 60;
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }

  String _formatPairingCode(String code) {
    final clean = code.replaceAll('-', '').trim().toUpperCase();
    if (clean.length == 8) {
      return '${clean.substring(0, 4)} - ${clean.substring(4)}';
    }
    return code;
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      titlePadding: const EdgeInsets.fromLTRB(24, 24, 24, 12),
      contentPadding: const EdgeInsets.symmetric(horizontal: 24, vertical: 8),
      actionsPadding: const EdgeInsets.fromLTRB(24, 12, 24, 20),
      title: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: const Color(0xFF25D366).withValues(alpha: 0.15),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(Icons.phonelink_setup_rounded, color: Color(0xFF25D366), size: 28),
          ),
          const SizedBox(width: 12),
          const Expanded(
            child: Text(
              'ربط رقم الواتساب بالرمز',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.help_outline_rounded, color: Color(0xFF2563EB), size: 22),
            tooltip: 'شرح الخطوات',
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(),
            onPressed: () {
              PageHelpDialog.show(
                context: context,
                title: 'طريقة ربط واتساب بالرمز',
                subtitle: 'خطوات ربط رقم هاتفك بالنظام مباشرة',
                steps: const [
                  HelpStep(
                    icon: Icons.vpn_key_rounded,
                    title: '1. طلب رمز الربط',
                    description: 'أدخل رقمك المسجل في واتساب واضغط "طلب رمز الربط"، ثم انسخ الرمز الظاهر.',
                  ),
                  HelpStep(
                    icon: Icons.devices_rounded,
                    title: '2. فتح الأجهزة المرتبطة',
                    description: 'افتح تطبيق واتساب على هاتفك، اضغط على القائمة (الثلاث نقاط أو الإعدادات) ⬅️ "الأجهزة المرتبطة".',
                  ),
                  HelpStep(
                    icon: Icons.phone_android_rounded,
                    title: '3. الربط برقم الهاتف',
                    description: 'اضغط "ربط جهاز"، ثم في أسفل الشاشة اختر "الربط باستخدام رقم الهاتف بدلاً من ذلك".',
                  ),
                  HelpStep(
                    icon: Icons.check_circle_rounded,
                    title: '4. إدخال الرمز',
                    description: 'أدخل الرمز المكون من 8 خانات؛ سيتصل النظام بالواتساب تلقائياً خلال لحظات!',
                  ),
                ],
              );
            },
          ),
        ],
      ),
      content: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (_errorMessage != null)
                Container(
                  margin: const EdgeInsets.only(bottom: 16),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.red.shade50,
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: Colors.red.shade200),
                  ),
                  child: Row(
                    children: [
                      const Icon(Icons.error_outline, color: Colors.red, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          _errorMessage!,
                          style: TextStyle(color: Colors.red.shade800, fontSize: 13),
                        ),
                      ),
                    ],
                  ),
                ),

              if (_isLinkedSuccessfully) ...[
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 24.0),
                  child: Column(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: Colors.green.shade50,
                          shape: BoxShape.circle,
                          border: Border.all(color: Colors.green.shade200, width: 2),
                        ),
                        child: const Icon(Icons.check_circle_rounded, color: Color(0xFF10B981), size: 56),
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'تم ربط واتساب بنجاح! 🎉',
                        style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
                      ),
                      const SizedBox(height: 6),
                      const Text(
                        'تم إقران حسابك بالنظام، جاري حفظ البيانات...',
                        style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                ),
              ]
              else if (_isLoading) ...[
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 32.0),
                  child: Column(
                    children: [
                      const SizedBox(
                        width: 48,
                        height: 48,
                        child: CircularProgressIndicator(
                          strokeWidth: 3.5,
                          valueColor: AlwaysStoppedAnimation<Color>(Color(0xFF25D366)),
                        ),
                      ),
                      const SizedBox(height: 20),
                      Text(
                        _loadingMessage ?? 'جاري معالجة الطلب...',
                        style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: Color(0xFF1E293B)),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'يتم الآن فتح اتصال مشفر ومباشر مع سيرفرات واتساب، قد يستغرق ذلك بضع ثوانٍ...',
                        style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                ),
              ]
              else if (_pairingCode == null) ...[
                const Text(
                  'أدخل رقم هاتفك المسجل في واتساب، وسيقوم النظام بتوليد رمز ربط فوري لإدخاله في هاتفك:',
                  style: TextStyle(fontSize: 13.5, color: Color(0xFF475569), height: 1.4),
                ),
                const SizedBox(height: 16),
                Form(
                  key: _formKey,
                  child: CountryPhoneInput(
                    controller: _phoneController,
                    initialCountry: _selectedCountry,
                    onCountryChanged: (c) => setState(() => _selectedCountry = c),
                  ),
                ),
                const SizedBox(height: 20),
                SizedBox(
                  height: 48,
                  child: ElevatedButton(
                    onPressed: () => _requestPairingCode(forceReset: false),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF25D366),
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                      elevation: 0,
                    ),
                    child: const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.key_rounded, size: 20),
                        SizedBox(width: 8),
                        Text(
                          'طلب رمز الربط 📲',
                          style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                TextButton(
                  onPressed: () => _requestPairingCode(forceReset: true),
                  child: const Text(
                    'هل تواجه مشكلة؟ اضغط لإعادة تعيين الجلسة والربط من جديد',
                    style: TextStyle(fontSize: 12, color: Color(0xFF2563EB)),
                    textAlign: TextAlign.center,
                  ),
                ),
                const SizedBox(height: 4),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('إلغاء', style: TextStyle(color: Colors.grey, fontSize: 14)),
                ),
              ]
              else ...[
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                  decoration: BoxDecoration(
                    color: _secondsRemaining > 30 
                        ? const Color(0xFFF1F5F9) 
                        : (_secondsRemaining > 0 ? Colors.amber.shade50 : Colors.red.shade50),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(
                      color: _secondsRemaining > 30 
                          ? const Color(0xFFCBD5E1) 
                          : (_secondsRemaining > 0 ? Colors.amber.shade300 : Colors.red.shade300),
                    ),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        _secondsRemaining > 0 ? Icons.timer_outlined : Icons.timer_off_outlined,
                        size: 18,
                        color: _secondsRemaining > 30 
                            ? const Color(0xFF475569) 
                            : (_secondsRemaining > 0 ? Colors.amber.shade900 : Colors.red.shade800),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          _secondsRemaining > 0
                              ? 'صلاحية الرمز تنتهي خلال: ${_formatTimer(_secondsRemaining)}'
                              : 'انتهت صلاحية الرمز! يرجى تجديده أدناه.',
                          style: TextStyle(
                            fontSize: 12.5,
                            fontWeight: FontWeight.bold,
                            color: _secondsRemaining > 30 
                                ? const Color(0xFF334155) 
                                : (_secondsRemaining > 0 ? Colors.amber.shade900 : Colors.red.shade900),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),

                Container(
                  padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 16),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF8FAFC),
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(
                      color: _secondsRemaining > 0 ? const Color(0xFF2563EB) : Colors.grey.shade400, 
                      width: 2,
                    ),
                    boxShadow: [
                      BoxShadow(
                        color: const Color(0xFF2563EB).withOpacity(0.06),
                        blurRadius: 10,
                        offset: const Offset(0, 4),
                      ),
                    ],
                  ),
                  child: Column(
                    children: [
                      const Text(
                        'رمز الربط الخاص بك:',
                        style: TextStyle(fontSize: 13, color: Color(0xFF64748B), fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: 8),
                      SelectableText(
                        _formatPairingCode(_pairingCode!),
                        style: TextStyle(
                          fontSize: 32, 
                          fontWeight: FontWeight.w900, 
                          letterSpacing: 4,
                          color: _secondsRemaining > 0 ? const Color(0xFF0F172A) : Colors.grey,
                          fontFamily: 'monospace',
                        ),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 12),
                      InkWell(
                        onTap: _secondsRemaining > 0 ? _copyToClipboard : null,
                        borderRadius: BorderRadius.circular(8),
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 7),
                          decoration: BoxDecoration(
                            color: _isCopied ? Colors.green.shade50 : Colors.white,
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: _isCopied ? Colors.green : Colors.grey.shade300),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(
                                _isCopied ? Icons.check : Icons.copy_rounded,
                                size: 16,
                                color: _isCopied ? Colors.green.shade800 : Colors.black87,
                              ),
                              const SizedBox(width: 6),
                              Text(
                                _isCopied ? 'تم النسخ بنجاح!' : 'نسخ الرمز',
                                style: TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.bold,
                                  color: _isCopied ? Colors.green.shade800 : Colors.black87,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),

                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFFEFF6FF),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: const Color(0xFFBFDBFE)),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Row(
                        children: [
                          Icon(Icons.info_outline, color: Color(0xFF2563EB), size: 16),
                          SizedBox(width: 6),
                          Text(
                            'خطوات إدخال الرمز في هاتفك:',
                            style: TextStyle(color: Color(0xFF1E40AF), fontWeight: FontWeight.bold, fontSize: 12.5),
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      _buildStep(1, 'افتح تطبيق واتساب في هاتفك وانتقل إلى "الأجهزة المرتبطة".'),
                      _buildStep(2, 'اضغط على "ربط جهاز".'),
                      _buildStep(3, 'في أسفل الشاشة اضغط على "الربط باستخدام رقم الهاتف".'),
                      _buildStep(4, 'أدخل الرمز الموضح أعلاه؛ وسيربط النظام تلقائياً.'),
                    ],
                  ),
                ),
                const SizedBox(height: 14),

                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: () => _requestPairingCode(forceReset: true),
                        icon: const Icon(Icons.refresh_rounded, size: 16),
                        label: Text(
                          _secondsRemaining <= 0 ? 'تجديد الرمز الآن' : 'طلب رمز جديد',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: _secondsRemaining <= 0 ? Colors.red.shade700 : const Color(0xFF2563EB),
                          side: BorderSide(
                            color: _secondsRemaining <= 0 ? Colors.red.shade400 : const Color(0xFF2563EB),
                          ),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(vertical: 10),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('إغلاق', style: TextStyle(color: Colors.grey, fontSize: 13)),
                    ),
                  ],
                ),
              ]
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStep(int number, String text) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 8,
            backgroundColor: const Color(0xFF2563EB),
            child: Text(number.toString(), style: const TextStyle(color: Colors.white, fontSize: 9, fontWeight: FontWeight.bold)),
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: const TextStyle(fontSize: 11.5, color: Color(0xFF1E293B)))),
        ],
      ),
    );
  }
}