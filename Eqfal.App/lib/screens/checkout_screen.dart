import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import '../models/subscription_models.dart';
import '../services/subscription_service.dart';

class CheckoutScreen extends StatefulWidget {
  final SubscriptionPlanItem plan;
  final PlanPriceItem price;
  final int cycle;
  final bool confirmReplace;

  const CheckoutScreen({
    super.key,
    required this.plan,
    required this.price,
    required this.cycle,
    required this.confirmReplace,
  });

  @override
  State<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends State<CheckoutScreen> {
  final SubscriptionService _subService = SubscriptionService();
  final TextEditingController _codeController = TextEditingController();

  bool _isValidatingCode = false;
  String? _codeError;
  DiscountValidationResult? _discountResult;

  bool _isProcessingPayment = false;
  String? _activePaymentId;
  Timer? _pollingTimer;

  @override
  void dispose() {
    _pollingTimer?.cancel();
    _codeController.dispose();
    super.dispose();
  }

  String _getCycleTitle(int cycle) {
    switch (cycle) {
      case 1: return 'شهر (30 يوماً)';
      case 2: return '3 شهور (90 يوماً)';
      case 3: return '6 شهور (180 يوماً)';
      case 4: return 'سنة (365 يوماً)';
      default: return '';
    }
  }

  void _validateCode() async {
    final code = _codeController.text.trim();
    if (code.isEmpty) return;

    setState(() {
      _isValidatingCode = true;
      _codeError = null;
    });

    try {
      final res = await _subService.validateDiscount(widget.price.id, code);
      if (mounted) {
        setState(() {
          _discountResult = res;
          _isValidatingCode = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _discountResult = null;
          _codeError = e.toString().replaceAll('Exception: ', '');
          _isValidatingCode = false;
        });
      }
    }
  }

  void _clearDiscount() {
    setState(() {
      _discountResult = null;
      _codeError = null;
      _codeController.clear();
    });
  }

  void _startPayment() async {
    setState(() {
      _isProcessingPayment = true;
    });

    try {
      final res = await _subService.checkout(
        planPriceId: widget.price.id,
        discountCode: _discountResult?.code,
        confirmReplaceActivePlan: widget.confirmReplace,
      );

      final isFree = res['isFree'] == true;
      final paymentId = res['id'];
      final paymentUrl = res['paymentUrl'];

      if (isFree) {
        if (!mounted) return;
        setState(() => _isProcessingPayment = false);
        _showSuccessDialog('تم تفعيل اشتراكك بنجاح ومجاناً بخصم 100%!');
        return;
      }

      if (paymentUrl != null && paymentUrl.isNotEmpty) {
        _activePaymentId = paymentId;
        final uri = Uri.parse(paymentUrl);
        if (await canLaunchUrl(uri)) {
          await launchUrl(uri, mode: LaunchMode.externalApplication);
        }

        // بدء استعلام الحالة التلقائي (Polling) كل 4 ثوانٍ
        _startPolling(paymentId);
        _showWaitingModal();
      } else {
        throw Exception('تعذر استلام رابط الدفع من EzonePay');
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isProcessingPayment = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(e.toString().replaceAll('Exception: ', ''), textAlign: TextAlign.right),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  void _startPolling(String paymentId) {
    _pollingTimer?.cancel();
    int attempts = 0;
    const maxAttempts = 225; // 15 دقيقة كحد أقصى

    _pollingTimer = Timer.periodic(const Duration(seconds: 4), (timer) async {
      attempts++;
      if (attempts > maxAttempts) {
        timer.cancel();
        return;
      }

      final status = await _subService.getPaymentStatus(paymentId);
      if (status == 'Settled') {
        timer.cancel();
        if (mounted) {
          Navigator.of(context, rootNavigator: true).pop(); // إغلاق نافذة الانتظار
          _showSuccessDialog('تم استلام وتأكيد سدادك بنجاح! اشتراكك مفعل الآن.');
        }
      } else if (status == 'Cancelled' || status == 'Rejected') {
        timer.cancel();
        if (mounted) {
          Navigator.of(context, rootNavigator: true).pop();
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم إلغاء عملية الدفع', textAlign: TextAlign.right)),
          );
          setState(() => _isProcessingPayment = false);
        }
      }
    });
  }

  void _showWaitingModal() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox(height: 12),
            const SizedBox(
              width: 48,
              height: 48,
              child: CircularProgressIndicator(color: Color(0xFF0284C7), strokeWidth: 3.5),
            ),
            const SizedBox(height: 20),
            const Text(
              'جارٍ انتظار إتمام الدفع...',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
            ),
            const SizedBox(height: 8),
            const Text(
              'تم فتح صفحة الدفع في EzonePay. يرجى إتمام السداد بالبطاقة أو سداد وسنفعّل حسابك فوراً.',
              style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: () async {
                if (_activePaymentId != null) {
                  final s = await _subService.getPaymentStatus(_activePaymentId!);
                  if (!mounted) return;
                  if (s == 'Settled') {
                    _pollingTimer?.cancel();
                    if (ctx.mounted) Navigator.pop(ctx);
                    _showSuccessDialog('تم تأكيد الدفع بنجاح!');
                    return;
                  }
                }
                if (!mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('العملية ما زالت قيد المعالجة، يرجى إتمام الدفع أولاً', textAlign: TextAlign.right)),
                );
              },
              icon: const Icon(Icons.refresh, size: 18),
              label: const Text('تحقّقت من الدفع'),
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF0284C7),
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            ),
            TextButton(
              onPressed: () {
                _pollingTimer?.cancel();
                Navigator.pop(ctx);
                setState(() => _isProcessingPayment = false);
              },
              child: const Text('إغلاق والعودة', style: TextStyle(color: Color(0xFF94A3B8))),
            )
          ],
        ),
      ),
    );
  }

  void _showSuccessDialog(String message) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Column(
          children: [
            Icon(Icons.check_circle, color: Color(0xFF10B981), size: 56),
            SizedBox(height: 12),
            Text('تهانينا!', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 20)),
          ],
        ),
        content: Text(
          message,
          style: const TextStyle(fontSize: 14, color: Color(0xFF334155)),
          textAlign: TextAlign.center,
        ),
        actions: [
          Center(
            child: ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF10B981),
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 12),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
              onPressed: () {
                Navigator.pop(ctx);
                context.go('/'); // العودة للشاشة الرئيسية
              },
              child: const Text('الانتقال للرئيسية', style: TextStyle(fontWeight: FontWeight.bold)),
            ),
          )
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final originalPrice = widget.price.price;
    final finalPrice = _discountResult != null ? _discountResult!.finalPrice : originalPrice;
    final discountAmount = _discountResult != null ? _discountResult!.discountAmount : 0.0;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: const Color(0xFFF8FAFC),
        appBar: AppBar(
          title: const Text('إتمام الدفع والاشتراك', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
          centerTitle: true,
          backgroundColor: Colors.white,
          elevation: 0.5,
          leading: IconButton(
            icon: const Icon(Icons.arrow_back_ios_new, size: 20, color: Color(0xFF0F172A)),
            onPressed: () => context.pop(),
          ),
        ),
        body: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            // بطاقة تفاصيل الخطة المختارة
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('تفاصيل الاشتراك', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                  const SizedBox(height: 14),
                  _buildDetailRow('الخطة:', widget.plan.nameAr),
                  const SizedBox(height: 10),
                  _buildDetailRow('الدورة:', _getCycleTitle(widget.cycle)),
                  const SizedBox(height: 10),
                  _buildDetailRow('مدة الاشتراك:', '${widget.price.durationDays} يوماً'),
                  const SizedBox(height: 10),
                  _buildDetailRow('الوصول:', 'مفتوح بالكامل لجميع الميزات'),
                ],
              ),
            ),

            const SizedBox(height: 20),

            // قسم كود الخصم
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.confirmation_number_outlined, size: 20, color: Color(0xFF0284C7)),
                      SizedBox(width: 8),
                      Text('كود الخصم (اختياري)', style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _codeController,
                          textCapitalization: TextCapitalization.characters,
                          decoration: InputDecoration(
                            hintText: 'أدخل كود الخصم هنا',
                            hintStyle: const TextStyle(fontSize: 13, color: Color(0xFF94A3B8)),
                            contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(10),
                              borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(10),
                              borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
                            ),
                            focusedBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(10),
                              borderSide: const BorderSide(color: Color(0xFF0284C7)),
                            ),
                          ),
                          onSubmitted: (_) => _validateCode(),
                        ),
                      ),
                      const SizedBox(width: 10),
                      ElevatedButton(
                        onPressed: _isValidatingCode ? null : _validateCode,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF0284C7),
                          foregroundColor: Colors.white,
                          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                        ),
                        child: _isValidatingCode
                            ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                            : const Text('تحقق', style: TextStyle(fontWeight: FontWeight.bold)),
                      ),
                    ],
                  ),
                  if (_codeError != null) ...[
                    const SizedBox(height: 8),
                    Text(_codeError!, style: const TextStyle(color: Color(0xFFE11D48), fontSize: 12)),
                  ],
                  if (_discountResult != null) ...[
                    const SizedBox(height: 10),
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: const Color(0xFFECFDF5),
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: const Color(0xFFA7F3D0)),
                      ),
                      child: Row(
                        children: [
                          const Icon(Icons.check_circle, color: Color(0xFF059669), size: 18),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'تم تطبيق الخصم بنجاح: وفّرت ${discountAmount.toStringAsFixed(discountAmount.truncateToDouble() == discountAmount ? 0 : 2)} د.ل',
                              style: const TextStyle(color: Color(0xFF065F46), fontSize: 13, fontWeight: FontWeight.bold),
                            ),
                          ),
                          IconButton(
                            icon: const Icon(Icons.close, size: 16, color: Color(0xFF065F46)),
                            onPressed: _clearDiscount,
                            padding: EdgeInsets.zero,
                            constraints: const BoxConstraints(),
                          )
                        ],
                      ),
                    )
                  ]
                ],
              ),
            ),

            const SizedBox(height: 20),

            // صندوق ملخص الفاتورة النهائي
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text('السعر الأصلي:', style: TextStyle(color: Color(0xFF64748B), fontSize: 14)),
                      Text(
                        '${originalPrice.toStringAsFixed(originalPrice.truncateToDouble() == originalPrice ? 0 : 2)} د.ل',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                          color: _discountResult != null ? const Color(0xFF94A3B8) : const Color(0xFF0F172A),
                          decoration: _discountResult != null ? TextDecoration.lineThrough : null,
                        ),
                      ),
                    ],
                  ),
                  if (_discountResult != null) ...[
                    const SizedBox(height: 10),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text('قيمة الخصم:', style: TextStyle(color: Color(0xFF059669), fontSize: 14)),
                        Text(
                          '- ${discountAmount.toStringAsFixed(discountAmount.truncateToDouble() == discountAmount ? 0 : 2)} د.ل',
                          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: Color(0xFF059669)),
                        ),
                      ],
                    ),
                  ],
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Divider(height: 1, color: Color(0xFFF1F5F9)),
                  ),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text('المجموع النهائي المطلوب:', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF0F172A))),
                      Text(
                        '${finalPrice.toStringAsFixed(finalPrice.truncateToDouble() == finalPrice ? 0 : 2)} د.ل',
                        style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0284C7)),
                      ),
                    ],
                  ),
                ],
              ),
            ),

            const SizedBox(height: 28),

            // زر الدفع الكبير
            ElevatedButton(
              onPressed: _isProcessingPayment ? null : _startPayment,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF0284C7),
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 16),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                elevation: 1,
              ),
              child: _isProcessingPayment
                  ? const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2)),
                        SizedBox(width: 12),
                        Text('جارٍ الاتصال ببوابة EzonePay...', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                      ],
                    )
                  : Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.payment, size: 22),
                        const SizedBox(width: 8),
                        Text(
                          'الدفع الآن عبر EzonePay (${finalPrice.toStringAsFixed(finalPrice.truncateToDouble() == finalPrice ? 0 : 2)} د.ل)',
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
            ),

            const SizedBox(height: 16),
            const Center(
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.lock, size: 14, color: Color(0xFF94A3B8)),
                  SizedBox(width: 4),
                  Text('دفع إلكتروني آمن ومحمي 100% عبر EzonePay', style: TextStyle(fontSize: 12, color: Color(0xFF94A3B8))),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDetailRow(String title, String value) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: const TextStyle(color: Color(0xFF64748B), fontSize: 14)),
        Text(value, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF0F172A))),
      ],
    );
  }
}
