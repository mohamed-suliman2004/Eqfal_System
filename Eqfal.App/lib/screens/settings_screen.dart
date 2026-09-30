import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../providers/operations_provider.dart';
import 'link_whatsapp_dialog.dart';
import 'account_details_dialog.dart';
import '../services/signalr_service.dart';
import '../providers/monitored_numbers_provider.dart';
import 'support_ticket_dialog.dart';
import '../services/biometric_service.dart';

class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  bool _isBiometricAvailable = false;
  bool _isBiometricEnabled = false;

  @override
  void initState() {
    super.initState();
    _loadBiometricState();
  }

  Future<void> _loadBiometricState() async {
    final bioService = ref.read(biometricServiceProvider);
    final available = await bioService.isBiometricAvailable();
    final enabled = await bioService.isBiometricLoginEnabled();
    if (mounted) {
      setState(() {
        _isBiometricAvailable = available;
        _isBiometricEnabled = enabled;
      });
    }
  }

  Future<void> _handleBiometricToggle(bool value) async {
    final bioService = ref.read(biometricServiceProvider);
    if (!value) {
      await bioService.disableBiometricLogin();
      if (mounted) {
        setState(() => _isBiometricEnabled = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم تعطيل الدخول بالبصمة', textAlign: TextAlign.right)),
        );
      }
      return;
    }

    final available = await bioService.isBiometricAvailable();
    if (!available) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('الجهاز لا يدعم البصمة أو لم يتم إعداد بصمة في إعدادات الهاتف', textAlign: TextAlign.right),
            backgroundColor: Color(0xFFE11D48),
          ),
        );
      }
      return;
    }

    final creds = await bioService.getBiometricCredentials();
    if (creds != null) {
      final authed = await bioService.authenticate();
      if (authed) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setBool('biometric_enabled', true);
        if (mounted) {
          setState(() => _isBiometricEnabled = true);
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم تفعيل الدخول بالبصمة بنجاح', textAlign: TextAlign.right), backgroundColor: Color(0xFF10B981)),
          );
        }
      }
      return;
    }

    _showEnableBiometricPasswordDialog();
  }

  void _showEnableBiometricPasswordDialog() async {
    final passwordCtrl = TextEditingController();
    bool obscure = true;
    bool isVerifying = false;

    final apiService = ref.read(apiServiceProvider);
    final profile = await apiService.getUserProfile();
    final username = profile?['usernameEmail'] ?? profile?['phone'] ?? '';

    if (!mounted) return;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: Row(
            children: const [
              Icon(Icons.fingerprint, color: Color(0xFF2563EB), size: 28),
              SizedBox(width: 8),
              Text('تفعيل تسجيل الدخول بالبصمة', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'يرجى إدخال كلمة المرور الحالية لتأكيد الهوية وحفظ البصمة على جهازك:',
                style: TextStyle(fontSize: 13, color: Color(0xFF475569)),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: passwordCtrl,
                obscureText: obscure,
                textDirection: TextDirection.ltr,
                decoration: InputDecoration(
                  labelText: 'كلمة المرور',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(obscure ? Icons.visibility_off : Icons.visibility),
                    onPressed: () => setDialogState(() => obscure = !obscure),
                  ),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('إلغاء', style: TextStyle(color: Color(0xFF64748B))),
            ),
            ElevatedButton(
              onPressed: isVerifying
                  ? null
                  : () async {
                      final pwd = passwordCtrl.text;
                      if (pwd.isEmpty) return;

                      setDialogState(() => isVerifying = true);
                      try {
                        final loginOk = await apiService.login(username, pwd);
                        if (!loginOk) {
                          if (ctx.mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(content: Text('كلمة المرور غير صحيحة', textAlign: TextAlign.right), backgroundColor: Color(0xFFE11D48)),
                            );
                            setDialogState(() => isVerifying = false);
                          }
                          return;
                        }

                        final bioService = ref.read(biometricServiceProvider);
                        final authed = await bioService.authenticate();
                        if (authed) {
                          await bioService.saveBiometricCredentials(username, pwd);
                          if (ctx.mounted) {
                            Navigator.pop(ctx);
                            setState(() => _isBiometricEnabled = true);
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(content: Text('تم تفعيل الدخول بالبصمة بنجاح', textAlign: TextAlign.right), backgroundColor: Color(0xFF10B981)),
                            );
                          }
                        } else {
                          setDialogState(() => isVerifying = false);
                        }
                      } catch (e) {
                        setDialogState(() => isVerifying = false);
                        if (ctx.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('كلمة المرور غير صحيحة', textAlign: TextAlign.right), backgroundColor: Color(0xFFE11D48)),
                          );
                        }
                      }
                    },
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF2563EB),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
              child: isVerifying
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Text('تأكيد وتفعيل', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('الإعدادات', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.black87)),
        centerTitle: true,
        backgroundColor: const Color(0xFFF8FAFC),
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.black87),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
        child: Column(
          children: [
            _buildSettingsTile(
              context: context,
              title: 'الاشتراك والأسعار',
              subtitle: 'إدارة وتجديد اشتراكك واختيار دورة الدفع (شهر، 3، 6، سنة).',
              icon: Icons.workspace_premium_outlined,
              onTap: () => context.push('/subscriptions'),
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'ربط واتساب',
              subtitle: 'اربط حساب واتساب لمعالجة الرسائل تلقائياً.',
              icon: Icons.chat_bubble_outline,
              onTap: () {
                showDialog(
                  context: context,
                  barrierDismissible: false,
                  builder: (context) => const LinkWhatsAppDialog(),
                );
              },
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'القواميس',
              subtitle: 'أدر كلمات التصنيف لكل نوع عملية.',
              icon: Icons.menu_book_outlined,
              onTap: () => context.push('/dictionaries'),
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'الأرقام المراقبة',
              subtitle: 'أدر الأرقام والمجموعات التي تريد مراقبتها.',
              icon: Icons.phonelink_ring_outlined,
              onTap: () => context.push('/monitored-numbers'),
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'معلومات الحساب',
              subtitle: 'عرض بيانات حسابك وتعديلها.',
              icon: Icons.person_outline,
              onTap: () {
                showDialog(
                  context: context,
                  builder: (context) => const AccountDetailsDialog(),
                );
              },
            ),
            const SizedBox(height: 12),
            _buildSwitchTile(
              title: 'تسجيل الدخول بالبصمة',
              subtitle: _isBiometricAvailable
                  ? 'تفعيل الدخول السريع باستخدام البصمة أو الوجه'
                  : 'ميزة البصمة غير متوفرة أو غير مفعلة في جهازك',
              icon: Icons.fingerprint,
              value: _isBiometricEnabled,
              onChanged: _handleBiometricToggle,
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'دليل استخدام إقفال',
              subtitle: 'استعرض خطوات عمل النظام وكيفية الاستفادة منه.',
              icon: Icons.menu_book_rounded,
              onTap: () => context.push('/onboarding?fromSettings=true'),
            ),
            const SizedBox(height: 12),
            _buildSettingsTile(
              context: context,
              title: 'الدعم الفني والمساعدة',
              subtitle: 'تواصل مع فريق الدعم لحل أي مشكلة.',
              icon: Icons.support_agent,
              onTap: () {
                showDialog(
                  context: context,
                  builder: (context) => const SupportTicketDialog(),
                );
              },
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: OutlinedButton(
                onPressed: () async {
                  ref.read(signalRServiceProvider).stopConnection();
                  await ref.read(apiServiceProvider).logout();
                  ref.invalidate(operationsProvider);
                  ref.invalidate(monitoredNumbersProvider);
                  if (mounted) {
                    context.go('/login');
                  }
                },
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: Colors.red, width: 1.5),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                  foregroundColor: Colors.red,
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: const [
                    Text('تسجيل الخروج', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                    SizedBox(width: 8),
                    Icon(Icons.logout, size: 20),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSwitchTile({
    required String title,
    required String subtitle,
    required IconData icon,
    required bool value,
    required ValueChanged<bool> onChanged,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade100, width: 2),
        boxShadow: [
          BoxShadow(color: Colors.black.withValues(alpha: 0.02), blurRadius: 10, offset: const Offset(0, 4)),
        ],
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: Colors.blue.shade50, shape: BoxShape.circle),
            child: Icon(icon, color: Colors.blue.shade700, size: 20),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF1E293B))),
                const SizedBox(height: 4),
                Text(subtitle, style: const TextStyle(fontSize: 12, color: Color(0xFF64748B))),
              ],
            ),
          ),
          Switch(
            value: value,
            activeThumbColor: const Color(0xFF2563EB),
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }

  Widget _buildSettingsTile({
    required BuildContext context,
    required String title,
    required String subtitle,
    required IconData icon,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.grey.shade100, width: 2),
          boxShadow: [
            BoxShadow(color: Colors.black.withValues(alpha: 0.02), blurRadius: 10, offset: const Offset(0, 4)),
          ],
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(color: Colors.blue.shade50, shape: BoxShape.circle),
              child: Icon(icon, color: Colors.blue.shade700, size: 20),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF1E293B))),
                  const SizedBox(height: 4),
                  Text(subtitle, style: const TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                ],
              ),
            ),
            const Icon(Icons.arrow_back_ios_new_rounded, size: 16, color: Color(0xFF94A3B8)),
          ],
        ),
      ),
    );
  }
}