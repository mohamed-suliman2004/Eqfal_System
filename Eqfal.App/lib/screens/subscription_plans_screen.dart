import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../models/subscription_models.dart';
import '../services/subscription_service.dart';

class SubscriptionPlansScreen extends StatefulWidget {
  const SubscriptionPlansScreen({super.key});

  @override
  State<SubscriptionPlansScreen> createState() => _SubscriptionPlansScreenState();
}

class _SubscriptionPlansScreenState extends State<SubscriptionPlansScreen> {
  final SubscriptionService _subService = SubscriptionService();

  bool _isLoading = true;
  String? _errorMessage;
  List<SubscriptionPlanItem> _plans = [];
  SubscriptionStatus? _currentStatus;

  // 1 = شهر (30 يوم), 4 = سنة (365 يوم)
  int _selectedCycle = 1;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final statusFuture = _subService.getCurrentSubscription();
      final plansFuture = _subService.getActiveCatalog();

      final results = await Future.wait([statusFuture, plansFuture]);
      final status = results[0] as SubscriptionStatus;
      final plans = results[1] as List<SubscriptionPlanItem>;

      if (mounted) {
        setState(() {
          _currentStatus = status;
          _plans = plans;
          if (status.billingCycle != null && (status.billingCycle == 1 || status.billingCycle == 4)) {
            _selectedCycle = status.billingCycle!;
          } else {
            _selectedCycle = 1;
          }
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = 'تعذر تحميل الخطط، يرجى المحاولة مرة أخرى';
          _isLoading = false;
        });
      }
    }
  }

  void _onSelectPlan(SubscriptionPlanItem plan, PlanPriceItem price) async {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => const Center(child: CircularProgressIndicator(color: Color(0xFF0284C7))),
    );

    try {
      final preview = await _subService.previewPlanChange(price.id);
      if (!mounted) return;
      Navigator.pop(context); // Close loading dialog

      if (preview.requiresConfirmation) {
        // حوار التأكيد الإلزامي لقاعدة D6
        _showD6ConfirmationDialog(plan, price, preview);
      } else {
        _navigateToCheckout(plan, price, false);
      }
    } catch (e) {
      if (!mounted) return;
      Navigator.pop(context); // Close loading dialog
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString(), textAlign: TextAlign.right), backgroundColor: Colors.red),
      );
    }
  }

  void _showD6ConfirmationDialog(
    SubscriptionPlanItem plan,
    PlanPriceItem price,
    PlanChangePreview preview,
  ) {
    final dateFormat = DateFormat('yyyy/MM/dd');
    final currentExpiryStr = preview.currentExpiresAt != null ? dateFormat.format(preview.currentExpiresAt!) : 'نهاية الفترة';
    final newExpiryStr = dateFormat.format(preview.newExpiresAt);

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.warning_amber_rounded, color: Color(0xFFD97706), size: 28),
            SizedBox(width: 8),
            Text(
              'تغيير خطة الاشتراك',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
              textAlign: TextAlign.right,
            ),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text.rich(
              TextSpan(
                style: const TextStyle(fontSize: 14, height: 1.6, color: Color(0xFF334155)),
                children: [
                  const TextSpan(text: 'اشتراكك الحالي في '),
                  TextSpan(
                    text: preview.currentPlanNameAr ?? 'خطتك الحالية',
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
                  ),
                  TextSpan(text: ' سارٍ حتى '),
                  TextSpan(
                    text: currentExpiryStr,
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
                  ),
                  TextSpan(text: '، ومتبقٍ منه '),
                  TextSpan(
                    text: '${preview.remainingDays} يوماً',
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFFD97706)),
                  ),
                  const TextSpan(text: '.\n\nبإتمام الدفع الآن '),
                  const TextSpan(
                    text: 'تنتهي خطتك الحالية فوراً',
                    style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFFE11D48)),
                  ),
                  TextSpan(text: ' وتبدأ الخطة الجديدة بمدة كاملة من اليوم حتى '),
                  TextSpan(
                    text: newExpiryStr,
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF0284C7)),
                  ),
                  const TextSpan(
                    text: '.\n\nالأيام المتبقية من خطتك الحالية لن تُضاف ولن تُسترد.',
                    style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF64748B)),
                  ),
                ],
              ),
              textAlign: TextAlign.right,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء', style: TextStyle(color: Color(0xFF64748B))),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF0284C7),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            ),
            onPressed: () {
              Navigator.pop(ctx);
              _navigateToCheckout(plan, price, true);
            },
            child: const Text('موافق، ابدأ الخطة الجديدة الآن', style: TextStyle(fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }

  void _navigateToCheckout(
    SubscriptionPlanItem plan,
    PlanPriceItem price,
    bool confirmReplace,
  ) {
    context.push('/checkout', extra: {
      'plan': plan,
      'price': price,
      'cycle': _selectedCycle,
      'confirmReplace': confirmReplace,
    });
  }


  SubscriptionPlanItem? get _proPlan {
    if (_plans.isEmpty) return null;
    final nonTrial = _plans.where((p) => !p.name.toLowerCase().contains('trial')).toList();
    return nonTrial.isNotEmpty ? nonTrial.first : _plans.first;
  }

  @override
  Widget build(BuildContext context) {
    final proPlan = _proPlan;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: const Color(0xFFF8FAFC),
        appBar: AppBar(
          title: const Text('خطط الاشتراك والأسعار', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
          centerTitle: true,
          backgroundColor: Colors.white,
          elevation: 0.5,
          leading: IconButton(
            icon: const Icon(Icons.arrow_back_ios_new, size: 20, color: Color(0xFF0F172A)),
            onPressed: () => context.pop(),
          ),
        ),
        body: _isLoading
            ? const Center(child: CircularProgressIndicator(color: Color(0xFF10B981)))
            : _errorMessage != null
                ? Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.error_outline, size: 48, color: Color(0xFFE11D48)),
                        const SizedBox(height: 12),
                        Text(_errorMessage!, style: const TextStyle(fontSize: 16)),
                        const SizedBox(height: 16),
                        ElevatedButton(
                          onPressed: _loadData,
                          style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF10B981)),
                          child: const Text('إعادة المحاولة', style: TextStyle(color: Colors.white)),
                        )
                      ],
                    ),
                  )
                : RefreshIndicator(
                    onRefresh: _loadData,
                    color: const Color(0xFF10B981),
                    child: ListView(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
                      children: [
                        // شريط حالة الاشتراك الحالي للمستخدم إن وجد
                        if (_currentStatus != null) _buildCurrentStatusBanner(),

                        const SizedBox(height: 24),

                        // مفتاح التبديل المتطابق مع الـ Landing Page (شهري / سنوي)
                        _buildBillingToggle(),

                        const SizedBox(height: 28),

                        // كرت الاشتراك الاحترافي (بدون كرت التجريبي وبدون شريط الأكثر طلباً)
                        if (proPlan != null)
                          Center(
                            child: ConstrainedBox(
                              constraints: const BoxConstraints(maxWidth: 540),
                              child: _buildProCard(proPlan),
                            ),
                          ),

                        const SizedBox(height: 32),
                      ],
                    ),
                  ),
      ),
    );
  }

  Widget _buildCurrentStatusBanner() {
    final status = _currentStatus!;
    Color bg = const Color(0xFFF1F5F9);
    Color border = const Color(0xFFCBD5E1);
    Color textCol = const Color(0xFF334155);
    String label = 'غير محدد';
    IconData icon = Icons.info_outline;

    if (status.status == 'Active') {
      bg = const Color(0xFFECFDF5);
      border = const Color(0xFFA7F3D0);
      textCol = const Color(0xFF065F46);
      label = 'اشتراك نشط';
      icon = Icons.verified;
    } else if (status.status == 'Trial') {
      bg = const Color(0xFFEFF6FF);
      border = const Color(0xFFBFDBFE);
      textCol = const Color(0xFF1E40AF);
      label = 'تجربة مجانية سارية';
      icon = Icons.hourglass_top;
    } else if (status.status == 'Grace') {
      bg = const Color(0xFFFFFBEB);
      border = const Color(0xFFFDE68A);
      textCol = const Color(0xFF92400E);
      label = 'فترة سماح (أوشك على الانتهاء)';
      icon = Icons.warning_amber_rounded;
    } else if (status.status == 'Expired') {
      bg = const Color(0xFFFFF1F2);
      border = const Color(0xFFFECDD3);
      textCol = const Color(0xFF9F1239);
      label = 'منتهي (وضع القراءة والتصدير فقط)';
      icon = Icons.lock_clock;
    }

    final dateFormat = DateFormat('yyyy/MM/dd');
    final expiryStr = status.expiresAt != null ? dateFormat.format(status.expiresAt!) : '—';

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: bg,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: border),
          ),
          child: Row(
            children: [
              Icon(icon, color: textCol, size: 24),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '$label: ${status.planType}',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: textCol),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'تاريخ الانتهاء: $expiryStr (${status.daysRemaining} يوم متبقي)',
                      style: TextStyle(fontSize: 12, color: textCol.withOpacity(0.85)),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildBillingToggle() {
    final isYearly = _selectedCycle == 4;

    return Column(
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            // شهري
            InkWell(
              onTap: () => setState(() => _selectedCycle = 1),
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Text(
                  'شهري',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: !isYearly ? FontWeight.bold : FontWeight.w600,
                    color: !isYearly ? const Color(0xFF0F172A) : const Color(0xFF94A3B8),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 12),

            // السويتش التفاعلي
            GestureDetector(
              onTap: () => setState(() => _selectedCycle = isYearly ? 1 : 4),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 220),
                curve: Curves.easeInOut,
                width: 52,
                height: 28,
                padding: const EdgeInsets.all(3),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(20),
                  color: isYearly ? const Color(0xFF10B981) : const Color(0xFFCBD5E1),
                ),
                child: AnimatedAlign(
                  duration: const Duration(milliseconds: 220),
                  curve: Curves.easeInOut,
                  alignment: isYearly ? Alignment.centerLeft : Alignment.centerRight,
                  child: Container(
                    width: 22,
                    height: 22,
                    decoration: const BoxDecoration(
                      shape: BoxShape.circle,
                      color: Colors.white,
                      boxShadow: [
                        BoxShadow(color: Colors.black12, blurRadius: 4, offset: Offset(0, 1)),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 12),

            // سنوي
            InkWell(
              onTap: () => setState(() => _selectedCycle = 4),
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Text(
                  'سنوي',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: isYearly ? FontWeight.bold : FontWeight.w600,
                    color: isYearly ? const Color(0xFF0F172A) : const Color(0xFF94A3B8),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 10),

            // شارة الخصم (وفر 30%)
            InkWell(
              onTap: () => setState(() => _selectedCycle = 4),
              borderRadius: BorderRadius.circular(20),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFFECFDF5),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: const Color(0xFFA7F3D0)),
                ),
                child: const Text(
                  'وفّر 30%',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF059669),
                  ),
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        AnimatedSwitcher(
          duration: const Duration(milliseconds: 200),
          child: Text(
            isYearly
                ? 'فاتورة سنوية تدفع كل 365 يوماً — الخيار الأوفر والأعلى قيمة'
                : 'فاتورة شهرية مرنة تتجدد كل 30 يوماً — بدون أي التزام طويل الأجل',
            key: ValueKey<bool>(isYearly),
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF64748B),
            ),
            textAlign: TextAlign.center,
          ),
        ),
      ],
    );
  }

  Widget _buildProCard(SubscriptionPlanItem plan) {
    final isYearly = _selectedCycle == 4;
    final priceItem = plan.getPriceForCycle(_selectedCycle);
    final isCurrentPlan = _currentStatus != null &&
        _currentStatus!.planId == plan.id &&
        _currentStatus!.billingCycle == _selectedCycle &&
        _currentStatus!.status != 'Expired';

    // حسابات السعر والنص
    final priceValue = priceItem?.price ?? (isYearly ? 420.0 : 50.0);
    final priceString = priceValue.truncateToDouble() == priceValue
        ? priceValue.toInt().toString()
        : priceValue.toStringAsFixed(0);

    final String periodSubtitle = isYearly
        ? 'لكل سنة كاملة (${(priceValue / 12).toStringAsFixed(0)} د.ل / شهر فقط)'
        : 'لكل شهر واحد ($priceString د.ل / شهر)';

    // قائمة المزايا الكاملة المطابقة لصفحة الهبوط (Landing Page)
    final List<Map<String, dynamic>> featuresList = [
      {'text': 'عدد محادثات ورسائل غير محدود إطلاقاً', 'isBold': true},
      {'text': 'قيود يومية وسندات قبض وصرف غير محدودة', 'isBold': true},
      {'text': 'سجل رقابي متقدم وتدقيق فوري ضد تعديل وحذف الرسائل', 'isBold': false},
      {'text': 'كشوف حساب معتمدة بشعار متجرك جاهزة للإرسال والطباعة', 'isBold': false},
      {'text': 'تصدير فوري وتلقائي لجداول الإكسل ودفتر الأستاذ', 'isBold': false},
      {'text': 'بوابة دفع إلكتروني محلية آمنة وموثوقة', 'isBold': false},
      {'text': 'فترة سماح 3 أيام مع إمكانية القراءة وتصدير التقارير دائماً', 'isBold': false},
      {'text': 'دعم فني مباشر وتحديثات برمجية مجانية مستمرة', 'isBold': false},
    ];

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(
          color: const Color(0xFF10B981),
          width: 2,
        ),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFF10B981).withOpacity(0.12),
            blurRadius: 30,
            offset: const Offset(0, 10),
          ),
          BoxShadow(
            color: Colors.black.withOpacity(0.04),
            blurRadius: 10,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      padding: const EdgeInsets.all(26),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // شارة إقفال برو (Pro) وشارة الخطة الحالية إن وجدت
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                decoration: BoxDecoration(
                  color: const Color(0xFF10B981).withOpacity(0.14),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0xFF10B981).withOpacity(0.3)),
                ),
                child: const Text(
                  'إقفال برو (Pro)',
                  style: TextStyle(
                    color: Color(0xFF059669),
                    fontSize: 13,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              if (isCurrentPlan)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: const Color(0xFFECFDF5),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: const Color(0xFFA7F3D0)),
                  ),
                  child: const Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.verified, size: 14, color: Color(0xFF059669)),
                      SizedBox(width: 4),
                      Text(
                        'خطتك الحالية',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF065F46),
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),

          const SizedBox(height: 16),

          // عنوان الخطة
          const Text(
            'المنظومة الاحترافية الشاملة',
            style: TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: Color(0xFF0F172A),
            ),
          ),

          const SizedBox(height: 8),

          // الوصف
          const Text(
            'الحل المحاسبي الذكي والمتكامل للتجار والشركات لإقفال الحسابات وإدارة المدفوعات عبر واتساب.',
            style: TextStyle(
              fontSize: 13.5,
              color: Color(0xFF64748B),
              height: 1.55,
            ),
          ),

          const SizedBox(height: 20),

          // السعر والعملة
          Row(
            crossAxisAlignment: CrossAxisAlignment.baseline,
            textBaseline: TextBaseline.alphabetic,
            children: [
              Text(
                priceString,
                style: const TextStyle(
                  fontSize: 46,
                  fontWeight: FontWeight.w900,
                  color: Color(0xFF0F172A),
                  letterSpacing: -1,
                ),
              ),
              const SizedBox(width: 8),
              const Text(
                'د.ل',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF64748B),
                ),
              ),
            ],
          ),

          const SizedBox(height: 4),

          // تفصيل دورة السعر
          Text(
            periodSubtitle,
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF94A3B8),
            ),
          ),

          // شارة التوفير السنوية
          if (isYearly) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: const Color(0xFFFEF3C7),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: const Color(0xFFFDE68A)),
              ),
              child: const Row(
                children: [
                  Icon(Icons.savings_outlined, size: 18, color: Color(0xFFD97706)),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'وفرت 180 د.ل سنوياً (خصم 30% — الأوفر والأعلى قيمة 🔥)',
                      style: TextStyle(
                        color: Color(0xFFB45309),
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],

          const SizedBox(height: 20),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          const SizedBox(height: 20),

          // قائمة المزايا
          ...featuresList.map(
            (f) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.done_all_rounded, size: 19, color: Color(0xFF10B981)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      f['text'] as String,
                      style: TextStyle(
                        fontSize: 13.5,
                        fontWeight: (f['isBold'] as bool) ? FontWeight.bold : FontWeight.w500,
                        color: const Color(0xFF334155),
                        height: 1.45,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),

          const SizedBox(height: 16),

          // زر الاشتراك
          SizedBox(
            height: 52,
            child: ElevatedButton(
              onPressed: priceItem == null ? null : () => _onSelectPlan(plan, priceItem),
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF10B981),
                foregroundColor: Colors.white,
                elevation: 2,
                shadowColor: const Color(0xFF10B981).withOpacity(0.35),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                disabledBackgroundColor: const Color(0xFFCBD5E1),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.bolt_rounded, size: 22),
                  const SizedBox(width: 8),
                  Text(
                    isCurrentPlan
                        ? 'تجديد الاشتراك الحالي'
                        : (isYearly ? 'اشترك سنوياً ووفر 30%' : 'اشترك الآن في إقفال برو'),
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
