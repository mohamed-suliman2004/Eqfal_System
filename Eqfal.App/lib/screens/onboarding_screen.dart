import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

class OnboardingScreen extends StatefulWidget {
  final bool isFromSettings;

  const OnboardingScreen({super.key, this.isFromSettings = false});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final PageController _pageController = PageController();
  int _currentPage = 0;

  final int _totalPages = 4;

  void _onFinish() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('has_seen_onboarding', true);

    if (!mounted) return;

    if (widget.isFromSettings) {
      context.pop();
    } else {
      context.go('/');
    }
  }

  void _nextPage() {
    if (_currentPage < _totalPages - 1) {
      _pageController.nextPage(
        duration: const Duration(milliseconds: 350),
        curve: Curves.easeInOut,
      );
    } else {
      _onFinish();
    }
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: const Color(0xFFF8FAFC),
        body: SafeArea(
          child: Column(
            children: [
              // Top Bar
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    if (widget.isFromSettings)
                      IconButton(
                        onPressed: () => context.pop(),
                        icon: const Icon(Icons.arrow_forward_ios_rounded, size: 20),
                        color: const Color(0xFF1E293B),
                      )
                    else
                      const SizedBox(width: 48),

                    Text(
                      widget.isFromSettings ? 'دليل استخدام إقفال' : 'إرشادات الاستخدام',
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF0F172A),
                      ),
                    ),

                    if (!widget.isFromSettings)
                      TextButton(
                        onPressed: _onFinish,
                        child: const Text(
                          'تخطي',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            color: Color(0xFF64748B),
                          ),
                        ),
                      )
                    else
                      const SizedBox(width: 48),
                  ],
                ),
              ),

              // Page Content
              Expanded(
                child: PageView(
                  controller: _pageController,
                  onPageChanged: (index) {
                    setState(() {
                      _currentPage = index;
                    });
                  },
                  children: [
                    _buildSlide1(),
                    _buildSlide2(),
                    _buildSlide3(),
                    _buildSlide4(),
                  ],
                ),
              ),

              // Bottom Navigation Bar
              Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  children: [
                    // Dot Indicators
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: List.generate(_totalPages, (index) {
                        final isActive = index == _currentPage;
                        return AnimatedContainer(
                          duration: const Duration(milliseconds: 250),
                          margin: const EdgeInsets.symmetric(horizontal: 4),
                          width: isActive ? 28 : 8,
                          height: 8,
                          decoration: BoxDecoration(
                            color: isActive ? const Color(0xFF2563EB) : const Color(0xFFCBD5E1),
                            borderRadius: BorderRadius.circular(4),
                          ),
                        );
                      }),
                    ),
                    const SizedBox(height: 24),

                    // Next / Finish Button
                    SizedBox(
                      width: double.infinity,
                      height: 52,
                      child: ElevatedButton(
                        onPressed: _nextPage,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF2563EB),
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                          elevation: 0,
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Text(
                              _currentPage == _totalPages - 1
                                  ? (widget.isFromSettings ? 'تم، العودة للإعدادات' : 'ابدأ الاستخدام الآن 🚀')
                                  : 'التالي ⬅️',
                              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                            ),
                          ],
                        ),
                      ),
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

  // Slide 1: Introduction
  Widget _buildSlide1() {
    return _buildSlideContainer(
      badgeText: 'مرحباً بك في إقفال',
      badgeColor: const Color(0xFF2563EB),
      badgeBgColor: const Color(0xFFEFF6FF),
      iconData: Icons.lock_clock_rounded,
      iconColor: const Color(0xFF2563EB),
      iconBgColor: const Color(0xFFDBEAFE),
      title: 'نظامك الذكي لإدارة العمليات والتحويلات',
      description: 'يقوم نظام إقفال بمراقبة محادثات الواتساب الخاصة بعملك، واستخراج الحوالات والعمليات اليومية وتصنيفها وحساب مبالغها تلقائياً وبشكل فوري دون أي إدخال يدوي.',
      contentWidget: Column(
        children: [
          _buildFeatureRow(Icons.bolt_rounded, 'معالجة مباشرة', 'قراءة الرسائل والتحويلات فور وصولها إلى هاتفك.'),
          _buildFeatureRow(Icons.calculate_outlined, 'إجماليات دقيقة', 'حساب فوري للأرباح والمبالغ اليومية والشهرية.'),
          _buildFeatureRow(Icons.security_rounded, 'أمان وتشفير عالي', 'حماية بياناتك والتحقق السريع عبر البصمة.'),
        ],
      ),
    );
  }

  // Slide 2: WhatsApp Pairing Code
  Widget _buildSlide2() {
    return _buildSlideContainer(
      badgeText: 'الخطوة 1: ربط الواتساب',
      badgeColor: const Color(0xFF16A34A),
      badgeBgColor: const Color(0xFFDCFCE7),
      iconData: Icons.phonelink_setup_rounded,
      iconColor: const Color(0xFF16A34A),
      iconBgColor: const Color(0xFFDCFCE7),
      title: 'ربط واتساب برمز الربط (Pair Code)',
      description: 'اربط واتساب الخاص بنشاطك التجاري بكل سهولة من نفس الهاتف ودون الحاجة لهاتف آخر:',
      contentWidget: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFFE2E8F0)),
        ),
        child: Column(
          children: [
            _buildNumberStep(1, 'من شاشة الإعدادات، اضغط على "ربط واتساب" واكتب رقمك لطلب الرمز.'),
            _buildNumberStep(2, 'انسخ رمز الربط (Pair Code) المكون من 8 خانات.'),
            _buildNumberStep(3, 'افتح واتساب ⬅️ الأجهزة المرتبطة ⬅️ ربط جهاز.'),
            _buildNumberStep(4, 'اختر من الأسفل: "الربط باستخدام رقم الهاتف"، والصق الرمز وسيتصل فوراً.'),
          ],
        ),
      ),
    );
  }

  // Slide 3: #إقفال
  Widget _buildSlide3() {
    return _buildSlideContainer(
      badgeText: 'الخطوة 2: تفعيل المراقبة الذكية',
      badgeColor: const Color(0xFF16A34A),
      badgeBgColor: const Color(0xFFDCFCE7),
      iconData: Icons.tag_rounded,
      iconColor: const Color(0xFF16A34A),
      iconBgColor: const Color(0xFFDCFCE7),
      title: 'كيف تبدأ بتعقب التحويلات عبر (#إقفال)؟',
      description: 'النظام يحترم خصوصيتك التامة؛ فهو لا يقرأ أي رسائل شخصية، بل يركز فقط على محادثات ومجموعات العمل التي تحددها بنفسك:',
      contentWidget: Column(
        children: [
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: const Color(0xFFF0FDF4),
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: const Color(0xFF86EFAC), width: 1.5),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
                      decoration: BoxDecoration(
                        color: const Color(0xFF16A34A),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: const Text(
                        '#إقفال',
                        style: TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w900,
                          fontSize: 16,
                          letterSpacing: 1,
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    const Expanded(
                      child: Text(
                        'طريقة التفعيل السريعة:',
                        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: Color(0xFF166534)),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                _buildExplainBullet(
                  Icons.send_rounded,
                  'ادخل على أي محادثة لزبون أو مجموعة (قروب) لزبائن يتم فيها تبادل عمليات التحويلات، وأرسل فيها رسالة تحتوي على: #إقفال',
                  isBold: true,
                ),
                const SizedBox(height: 12),
                _buildExplainBullet(
                  Icons.filter_alt_rounded,
                  'سيتم تلقائياً تعقب كل رسائل التحويلات المالية فقط داخل هذه المحادثة وتجاهل أي محادثات ورسائل أخرى.',
                ),
                const SizedBox(height: 12),
                _buildExplainBullet(
                  Icons.dashboard_customize_rounded,
                  'تُنظم وتوضع العمليات بشكل خاص عندك في التطبيق، مع تحديد التصنيف الخاص بها (استلام أو تسليم)، وقيمة المبلغ، والعملة، واسم المصرف، وكافة تفاصيل الرسالة.',
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          _buildFeatureRow(
            Icons.tune_rounded,
            'تحكم وإلغاء في أي وقت',
            'يمكنك مراجعة كافة المحادثات المراقبة، أو إيقاف تعقب أي محادثة وحذفها نهائياً من شاشة "الأرقام المراقبة".',
          ),
        ],
      ),
    );
  }

  // Slide 4: Operations, Anti-Tamper & Export
  Widget _buildSlide4() {
    return _buildSlideContainer(
      badgeText: 'الخطوة 3: العمليات والتقارير',
      badgeColor: const Color(0xFF7C3AED),
      badgeBgColor: const Color(0xFFF5F3FF),
      iconData: Icons.analytics_rounded,
      iconColor: const Color(0xFF7C3AED),
      iconBgColor: const Color(0xFFEDE9FE),
      title: 'العمليات والتقارير بين يديك',
      description: 'لوحة تحكم متكاملة تمنحك سيطرة كاملة وحماية من أي تلاعب في المبالغ أو الحوالات:',
      contentWidget: Column(
        children: [
          _buildFeatureRow(Icons.auto_stories_outlined, 'القواميس الذكية', 'تصنيف دقيق لكل عملية (إيداع، سحب، تداول) حسب كلماتك المفتاحية.'),
          _buildFeatureRow(Icons.edit_notifications_outlined, 'كشف تعديلات الواتساب', 'تنبيه فوري وعلامة (معدلة) عند تعديل أي زبون لرسالته بعد إرسالها.'),
          _buildFeatureRow(Icons.picture_as_pdf_outlined, 'تصدير وطباعة', 'استخراج كشوفات حساب وتقارير بصيغة PDF أو Excel بنقرة واحدة.'),
        ],
      ),
    );
  }

  // Slide Base Container
  Widget _buildSlideContainer({
    required String badgeText,
    required Color badgeColor,
    required Color badgeBgColor,
    required IconData iconData,
    required Color iconColor,
    required Color iconBgColor,
    required String title,
    required String description,
    required Widget contentWidget,
  }) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 8),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 580),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              const SizedBox(height: 8),
            // Graphic Icon Container
            Container(
              width: 90,
              height: 90,
              decoration: BoxDecoration(
                color: iconBgColor,
                shape: BoxShape.circle,
                boxShadow: [
                  BoxShadow(
                    color: iconColor.withValues(alpha: 0.15),
                    blurRadius: 20,
                    offset: const Offset(0, 8),
                  ),
                ],
              ),
              child: Icon(iconData, size: 44, color: iconColor),
            ),
            const SizedBox(height: 20),

            // Badge
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
              decoration: BoxDecoration(
                color: badgeBgColor,
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                badgeText,
                style: TextStyle(
                  color: badgeColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 13,
                ),
              ),
            ),
            const SizedBox(height: 12),

            // Title
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w900,
                color: Color(0xFF0F172A),
                height: 1.3,
              ),
            ),
            const SizedBox(height: 10),

            // Description
            Text(
              description,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 13.5,
                color: Color(0xFF64748B),
                height: 1.5,
              ),
            ),
            const SizedBox(height: 20),

            // Slide Specific Content
            contentWidget,
          ],
        ),
      ),
    ),
  );
}

  Widget _buildFeatureRow(IconData icon, String title, String subtitle) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: const Color(0xFFF1F5F9),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(icon, size: 20, color: const Color(0xFF2563EB)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.bold, color: Color(0xFF1E293B)),
                ),
                const SizedBox(height: 2),
                Text(
                  subtitle,
                  style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildExplainBullet(IconData icon, String text, {bool isBold = false}) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          margin: const EdgeInsets.only(top: 2),
          padding: const EdgeInsets.all(6),
          decoration: BoxDecoration(
            color: const Color(0xFF16A34A).withValues(alpha: 0.15),
            shape: BoxShape.circle,
          ),
          child: Icon(icon, size: 15, color: const Color(0xFF16A34A)),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            text,
            style: TextStyle(
              fontSize: 13.5,
              height: 1.5,
              color: isBold ? const Color(0xFF14532D) : const Color(0xFF334155),
              fontWeight: isBold ? FontWeight.bold : FontWeight.w500,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildNumberStep(int stepNum, String text) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 24,
            height: 24,
            decoration: BoxDecoration(
              color: const Color(0xFF16A34A).withValues(alpha: 0.15),
              shape: BoxShape.circle,
            ),
            alignment: Alignment.center,
            child: Text(
              '$stepNum',
              style: const TextStyle(
                color: Color(0xFF16A34A),
                fontWeight: FontWeight.w900,
                fontSize: 12,
              ),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(
                fontSize: 13,
                height: 1.4,
                color: Color(0xFF334155),
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
