import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../services/api_service.dart';
import '../services/signalr_service.dart';
import '../providers/operations_provider.dart';
import '../providers/monitored_numbers_provider.dart';
import '../models/country_code.dart';
import '../widgets/country_phone_input.dart';
import 'link_whatsapp_dialog.dart';

class AccountDetailsDialog extends ConsumerStatefulWidget {
  const AccountDetailsDialog({super.key});

  @override
  ConsumerState<AccountDetailsDialog> createState() => _AccountDetailsDialogState();
}

class _AccountDetailsDialogState extends ConsumerState<AccountDetailsDialog> {
  bool _isLoading = true;
  Map<String, dynamic>? _profileData;

  @override
  void initState() {
    super.initState();
    _fetchProfile();
  }

  Future<void> _fetchProfile() async {
    final apiService = ref.read(apiServiceProvider);
    final data = await apiService.getUserProfile();
    if (mounted) {
      setState(() {
        _profileData = data;
        _isLoading = false;
      });
    }
  }

  void _showEditProfileDialog() {
    final nameCtrl = TextEditingController(text: _profileData?['fullName'] ?? '');
    final emailCtrl = TextEditingController(text: _profileData?['usernameEmail'] ?? '');
    
    final currentPhone = _profileData?['phone'] ?? '';
    final detectedCountry = CountryCode.parseFromFullPhone(currentPhone);
    CountryCode selectedCountry = detectedCountry;
    
    String nationalPhone = currentPhone;
    if (currentPhone.startsWith(detectedCountry.dialCode)) {
      nationalPhone = currentPhone.substring(detectedCountry.dialCode.length);
    } else if (currentPhone.startsWith(detectedCountry.dialCode.replaceFirst('+', ''))) {
      nationalPhone = currentPhone.substring(detectedCountry.dialCode.replaceFirst('+', '').length);
    }
    final phoneCtrl = TextEditingController(text: nationalPhone);
    
    bool isSaving = false;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (context, setDialogState) {
          return AlertDialog(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
            title: const Text('تعديل البيانات الشخصية', textAlign: TextAlign.right, style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextField(
                    controller: nameCtrl,
                    textAlign: TextAlign.right,
                    decoration: InputDecoration(
                      labelText: 'الاسم الكامل',
                      prefixIcon: const Icon(Icons.person_outline),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                  const SizedBox(height: 16),
                  CountryPhoneInput(
                    controller: phoneCtrl,
                    initialCountry: selectedCountry,
                    onCountryChanged: (c) => selectedCountry = c,
                    hintText: 'رقم الهاتف...',
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: emailCtrl,
                    textAlign: TextAlign.right,
                    decoration: InputDecoration(
                      labelText: 'اسم المستخدم / البريد',
                      prefixIcon: const Icon(Icons.email_outlined),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(ctx).pop(),
                child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
              ),
              ElevatedButton(
                onPressed: isSaving ? null : () async {
                  final rawPhone = phoneCtrl.text.trim();
                  if (nameCtrl.text.trim().isEmpty || rawPhone.isEmpty) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('يرجى ملء الاسم ورقم الهاتف')),
                    );
                    return;
                  }

                  final fullPhone = '${selectedCountry.dialCode}$rawPhone'.replaceAll('+', '');

                  setDialogState(() => isSaving = true);
                  final res = await ref.read(apiServiceProvider).updateUserProfile(
                    fullName: nameCtrl.text.trim(),
                    phone: fullPhone,
                    usernameEmail: emailCtrl.text.trim(),
                  );
                  setDialogState(() => isSaving = false);

                  if (res['success'] == true) {
                    if (mounted) {
                      Navigator.of(ctx).pop();
                      _fetchProfile();
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('تم تحديث البيانات بنجاح')),
                      );
                    }
                  } else {
                    if (mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(content: Text(res['message'] ?? 'فشل التحديث')),
                      );
                    }
                  }
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF2563EB),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
                child: isSaving
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                    : const Text('حفظ التعديلات', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
              ),
            ],
          );
        },
      ),
    );
  }

  void _showDeleteAccountConfirmation() {
    bool isDeleting = false;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (context, setConfirmState) {
          return AlertDialog(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
            title: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: const [
                Text(
                  'حذف الحساب نهائياً',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Colors.red),
                ),
                SizedBox(width: 8),
                Icon(Icons.warning_amber_rounded, color: Colors.red, size: 28),
              ],
            ),
            content: const Text(
              'تحذير: سيتم حذف كافة العمليات والمسودات والأرقام المراقبة والكلمات المفتاحية وجلسة الواتساب نهائياً من النظام.\n\nلا يمكن التراجع عن هذا الإجراء. هل أنت متأكد؟',
              textAlign: TextAlign.right,
              textDirection: TextDirection.rtl,
              style: TextStyle(fontSize: 14, height: 1.5, color: Color(0xFF334155)),
            ),
            actions: [
              TextButton(
                onPressed: isDeleting ? null : () => Navigator.of(ctx).pop(),
                child: const Text('إلغاء', style: TextStyle(color: Colors.grey, fontWeight: FontWeight.bold)),
              ),
              ElevatedButton(
                onPressed: isDeleting ? null : () async {
                  setConfirmState(() => isDeleting = true);
                  final res = await ref.read(apiServiceProvider).deleteAccount();
                  setConfirmState(() => isDeleting = false);

                  if (res['success'] == true) {
                    if (mounted) {
                      Navigator.of(ctx).pop(); // Close confirm dialog
                      Navigator.of(context).pop(); // Close details dialog

                      // Clean up connections & log out
                      ref.read(signalRServiceProvider).stopConnection();
                      await ref.read(apiServiceProvider).logout();
                      ref.invalidate(operationsProvider);
                      ref.invalidate(monitoredNumbersProvider);

                      context.go('/login');
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text('تم حذف الحساب وبياناته بالكامل بنجاح'),
                          backgroundColor: Colors.green,
                        ),
                      );
                    }
                  } else {
                    if (mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(res['message'] ?? 'فشل في حذف الحساب'),
                          backgroundColor: Colors.red,
                        ),
                      );
                    }
                  }
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.red,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                ),
                child: isDeleting
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                    : const Text('نعم، احذف حسابي', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
              ),
            ],
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final bool isConnected = _profileData?['isWhatsAppConnected'] == true;
    final String rawStatus = _profileData?['whatsappStatus'] ?? '';
    final String statusText = isConnected 
        ? 'متصل' 
        : (rawStatus.isNotEmpty ? rawStatus : 'غير متصل');

    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      elevation: 0,
      backgroundColor: Colors.transparent,
      child: Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          color: const Color(0xFFF8FAFC),
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(color: Colors.black.withOpacity(0.1), blurRadius: 20, offset: const Offset(0, 10)),
          ],
        ),
        constraints: const BoxConstraints(maxWidth: 440),
        child: _isLoading
            ? const SizedBox(
                height: 200,
                child: Center(child: CircularProgressIndicator()),
              )
            : SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Header
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        IconButton(
                          icon: const Icon(Icons.close, color: Colors.grey),
                          onPressed: () => Navigator.of(context).pop(),
                        ),
                        const Text(
                          'تفاصيل الحساب',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF1E293B)),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    
                    // User Info Card
                    Container(
                      padding: const EdgeInsets.all(18),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(color: Colors.grey.shade100, width: 1.5),
                        boxShadow: [
                          BoxShadow(color: Colors.black.withOpacity(0.02), blurRadius: 10, offset: const Offset(0, 4)),
                        ],
                      ),
                      child: Column(
                        children: [
                          _buildInfoRow(Icons.person_outline, 'الاسم', _profileData?['fullName'] ?? 'غير محدد'),
                          const Divider(height: 22),
                          _buildInfoRow(Icons.phone_outlined, 'الهاتف', _profileData?['phone'] ?? 'غير محدد'),
                          const Divider(height: 22),
                          _buildInfoRow(Icons.email_outlined, 'البريد', _profileData?['usernameEmail'] ?? 'غير محدد'),
                          const Divider(height: 22),
                          _buildInfoRow(
                            Icons.verified_outlined,
                            'حالة الواتساب',
                            isConnected ? 'متصل ✅' : '$statusText ❌',
                            color: isConnected ? Colors.green : Colors.red,
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 18),
                    
                    // Action Buttons Row
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: _showEditProfileDialog,
                            icon: const Icon(Icons.edit_outlined, size: 18),
                            label: const Text('تعديل البيانات', style: TextStyle(fontWeight: FontWeight.bold)),
                            style: OutlinedButton.styleFrom(
                              foregroundColor: const Color(0xFF1E293B),
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                              padding: const EdgeInsets.symmetric(vertical: 12),
                              side: BorderSide(color: Colors.grey.shade300),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: ElevatedButton.icon(
                            onPressed: () {
                              Navigator.of(context).pop();
                              showDialog(
                                context: context,
                                builder: (ctx) => LinkWhatsAppDialog(onLinked: _fetchProfile),
                              );
                            },
                            icon: const Icon(Icons.qr_code_2_rounded, size: 18, color: Colors.white),
                            label: const Text('ربط واتساب', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFF25D366),
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                              padding: const EdgeInsets.symmetric(vertical: 12),
                              elevation: 0,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),

                    // Delete Account Button
                    SizedBox(
                      width: double.infinity,
                      child: TextButton.icon(
                        onPressed: _showDeleteAccountConfirmation,
                        icon: const Icon(Icons.delete_forever_outlined, color: Colors.red, size: 20),
                        label: const Text(
                          'حذف الحساب نهائياً',
                          style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                        style: TextButton.styleFrom(
                          padding: const EdgeInsets.symmetric(vertical: 10),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String title, String value, {Color? color}) {
    return Row(
      textDirection: TextDirection.rtl,
      children: [
        Icon(icon, size: 20, color: const Color(0xFF64748B)),
        const SizedBox(width: 10),
        Text(
          title,
          style: const TextStyle(fontWeight: FontWeight.w600, color: Color(0xFF64748B), fontSize: 14),
        ),
        const Spacer(),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.bold,
            color: color ?? const Color(0xFF1E293B),
            fontSize: 14,
          ),
        ),
      ],
    );
  }
}