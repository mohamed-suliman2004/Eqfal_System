import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../providers/operations_provider.dart';
import '../services/signalr_service.dart';
import '../providers/monitored_numbers_provider.dart';
import '../services/app_version_service.dart';
import '../widgets/page_help_dialog.dart';
import '../services/subscription_service.dart';
import '../models/subscription_models.dart';

import 'package:shared_preferences/shared_preferences.dart';

class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({super.key});

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  final SubscriptionService _subService = SubscriptionService();
  SubscriptionStatus? _subStatus;

  @override
  void initState() {
    super.initState();
    _updateFcmToken();
    _loadSubscriptionStatus();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        AppVersionService.checkVersion(context);
      }
    });
  }

  Future<void> _loadSubscriptionStatus() async {
    final status = await _subService.getCurrentSubscription();
    if (mounted) {
      setState(() {
        _subStatus = status;
      });
    }
  }

  Future<void> _updateFcmToken() async {
    final prefs = await SharedPreferences.getInstance();
    final fcmToken = prefs.getString('fcm_token');
    if (fcmToken != null) {
      await ref.read(apiServiceProvider).updateFcmToken(fcmToken);
    }
  }

  @override
  Widget build(BuildContext context) {
    final operationsAsyncValue = ref.watch(operationsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('\u0625\u0642\u0641\u0627\u0644', style: TextStyle(fontWeight: FontWeight.w900, color: Colors.black87, fontSize: 24)),
        centerTitle: true,
        backgroundColor: const Color(0xFFF8FAFC),
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.black87),
        leading: Builder(
          builder: (context) => PopupMenuButton<String>(
            icon: const Icon(Icons.person_outline),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            offset: const Offset(0, 45),
            onSelected: (value) {
              if (value == 'subscriptions') {
                context.push('/subscriptions').then((_) => _loadSubscriptionStatus());
              } else if (value == 'settings') {
                context.push('/settings').then((_) => _loadSubscriptionStatus());
              } else if (value == 'logout') {
                ref.read(signalRServiceProvider).stopConnection();
                ref.read(apiServiceProvider).logout().then((_) {
                  ref.invalidate(operationsProvider);
                  ref.invalidate(monitoredNumbersProvider);
                  context.go('/login');
                });
              }
            },
            itemBuilder: (context) => [
              PopupMenuItem(
                value: 'subscriptions',
                child: Row(
                  children: const [
                    Icon(Icons.workspace_premium_outlined, color: Color(0xFF0284C7)),
                    SizedBox(width: 12),
                    Text('الاشتراك والأسعار', style: TextStyle(fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
              PopupMenuItem(
                value: 'settings',
                child: Row(
                  children: const [
                    Icon(Icons.settings_outlined, color: Colors.black87),
                    SizedBox(width: 12),
                    Text('\u0627\u0644\u0625\u0639\u062f\u0627\u062f\u0627\u062a', style: TextStyle(fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
              const PopupMenuDivider(),
              PopupMenuItem(
                value: 'logout',
                child: Row(
                  children: const [
                    Icon(Icons.logout, color: Colors.red),
                    SizedBox(width: 12),
                    Text('\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c', style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
            ],
          ),
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.help_outline_rounded, color: Color(0xFF2563EB)),
            tooltip: 'دليل الاستخدام',
            onPressed: () {
              PageHelpDialog.show(
                context: context,
                title: 'كيف يعمل نظام إقفال؟',
                subtitle: 'دليل الاستخدام السريع للنظام',
                steps: const [
                  HelpStep(
                    icon: Icons.phonelink_setup_rounded,
                    title: '1. ربط واتساب (Pair Code)',
                    description: 'من الإعدادات، اربط حساب واتساب عبر رمز الربط المباشر من نفس الهاتف.',
                  ),
                  HelpStep(
                    icon: Icons.tag_rounded,
                    title: '2. تفعيل المراقبة (#إقفال)',
                    description: 'ادخل على أي شات أو قروب في الواتساب وأرسل كلمة #إقفال لينضم لقائمة المراقبة.',
                    highlightBadge: '#إقفال',
                  ),
                  HelpStep(
                    icon: Icons.bolt_rounded,
                    title: '3. المعالجة التلقائية الفورية',
                    description: 'يقرأ النظام فواتيرك ويصنفها ويحسب الأرباح فوراً في كروت الاستلام والتسليم أدناه.',
                  ),
                  HelpStep(
                    icon: Icons.picture_as_pdf_outlined,
                    title: '4. التقارير والتصدير',
                    description: 'اضغط على بطاقة الاستلام أو التسليم لاستعراض العمليات وتصديرها كـ PDF أو Excel.',
                  ),
                ],
              );
            },
          ),
        ],
      ),
      body: operationsAsyncValue.when(
        data: (operations) {
          int draftsCount = 0;
          int receiptCount = 0;
          int deliveryCount = 0;

          double receiptLyd = 0;
          double receiptUsd = 0;
          double receiptEur = 0;

          double deliveryLyd = 0;
          double deliveryUsd = 0;
          double deliveryEur = 0;

          DateTime? lastUpdate;

          for (var op in operations) {
            if (lastUpdate == null || op.createdAt.isAfter(lastUpdate)) {
              lastUpdate = op.createdAt;
            }

            if (op.isDeleted) {
              continue; // Do not include deleted / revoked messages in regular summary totals
            }

            final status = op.status.trim();
            // Drafts are ONLY incomplete messages marked as مسودة
            if (status == '\u0645\u0633\u0648\u062f\u0629' || status == 'draft') {
              draftsCount++;
              continue; // Do not include drafts in regular delivery/receipt summary totals
            }

            final amount = op.amount ?? 0.0;
            final curr = (op.currency ?? '').toUpperCase().trim();
            final isUsd = curr == 'USD';
            final isEur = curr == 'EUR';

            final cat = (op.category ?? '').trim();

            if (cat == '\u0627\u0633\u062a\u0644\u0627\u0645') {
              receiptCount++;
              if (isUsd) receiptUsd += amount;
              else if (isEur) receiptEur += amount;
              else receiptLyd += amount;
            } else if (cat == '\u062a\u0633\u0644\u064a\u0645') {
              deliveryCount++;
              if (isUsd) deliveryUsd += amount;
              else if (isEur) deliveryEur += amount;
              else deliveryLyd += amount;
            }
          }

          final timeString = lastUpdate != null
              ? DateFormat('hh:mm a').format(lastUpdate).replaceAll('AM', '\u0635').replaceAll('PM', '\u0645')
              : '--:--';

          return RefreshIndicator(
            onRefresh: () async => ref.refresh(operationsProvider),
            child: ListView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.access_time, size: 14, color: Colors.grey),
                    const SizedBox(width: 4),
                    Text(
                      '\u0622\u062e\u0631 \u0631\u0633\u0627\u0644\u0629 \u0645\u0639\u0627\u0644\u062c\u0629 $timeString',
                      style: const TextStyle(color: Colors.grey, fontSize: 12),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                
                // شريط تنبيه الاشتراك عند اقتراب الانتهاء أو عند انتهاء الاشتراك (Read-Only)
                if (_subStatus != null && (_subStatus!.isGrace || _subStatus!.isExpired || (_subStatus!.isTrial && _subStatus!.daysRemaining <= 3))) ...[
                  _buildSubscriptionAlertCard(_subStatus!, context),
                  const SizedBox(height: 16),
                ],

                // Top warning card: ONLY shown if there are actual incomplete drafts
                if (draftsCount > 0) ...[
                  _buildWarningCard(draftsCount, context),
                  const SizedBox(height: 16),
                ],

                // Receipt Card (استلام)
                _buildSummaryCard(
                  title: '\u0627\u0633\u062a\u0644\u0627\u0645',
                  count: receiptCount,
                  icon: Icons.arrow_downward,
                  lyd: receiptLyd,
                  usd: receiptUsd,
                  eur: receiptEur,
                  onTap: () {
                    context.push('/list/\u0627\u0633\u062a\u0644\u0627\u0645');
                  },
                ),
                const SizedBox(height: 16),

                // Delivery Card (تسليم)
                _buildSummaryCard(
                  title: '\u062a\u0633\u0644\u064a\u0645',
                  count: deliveryCount,
                  icon: Icons.arrow_upward,
                  lyd: deliveryLyd,
                  usd: deliveryUsd,
                  eur: deliveryEur,
                  onTap: () {
                    context.push('/list/\u062a\u0633\u0644\u064a\u0645');
                  },
                ),
                const SizedBox(height: 24),

                // Analyze Message Button
                InkWell(
                  onTap: () => context.push('/analyze'),
                  borderRadius: BorderRadius.circular(16),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFF818CF8).withOpacity(0.3), width: 1.5),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Icon(Icons.chevron_left, color: Color(0xFF94A3B8)),
                        Row(
                          children: [
                            Column(
                              crossAxisAlignment: CrossAxisAlignment.end,
                              children: const [
                                Text(
                                  '\u062a\u062d\u0644\u064a\u0644 \u0631\u0633\u0627\u0644\u0629',
                                  style: TextStyle(color: Color(0xFF1E293B), fontWeight: FontWeight.bold, fontSize: 16),
                                ),
                                SizedBox(height: 4),
                                Text(
                                  '\u0623\u062f\u0631\u062c \u0631\u0633\u0627\u0644\u0629 \u0644\u0627\u0633\u062a\u062e\u0631\u0627\u062c \u0628\u064a\u0627\u0646\u0627\u062a\u0647\u0627',
                                  style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12),
                                ),
                              ],
                            ),
                            const SizedBox(width: 16),
                            Container(
                              padding: const EdgeInsets.all(10),
                              decoration: BoxDecoration(
                                color: const Color(0xFFEEF2FF),
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: const Icon(Icons.auto_awesome, color: Color(0xFF818CF8), size: 24),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 24),
              ],
            ),
          );
        },
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, stack) => Center(child: Text('\u062e\u0637\u0623: $error')),
      ),
    );
  }

  Widget _buildWarningCard(int count, BuildContext context) {
    return InkWell(
      onTap: () => context.push('/drafts'),
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
        decoration: BoxDecoration(
          color: const Color(0xFFFEF3C7),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFFFDE68A)),
        ),
        child: Row(
          children: [
            const Icon(Icons.warning_amber_rounded, color: Color(0xFFD97706)),
            const Spacer(),
            Column(
              children: [
                const Text(
                  '\u0645\u0633\u0648\u062f\u0627\u062a', // مسودات
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFFB45309)),
                ),
                Text(
                  '$count \u0645\u0633\u0648\u062f\u0629 \u0628\u0627\u0646\u062a\u0638\u0627\u0631 \u0627\u0644\u0627\u0633\u062a\u0643\u0645\u0627\u0644', // X مسودة بانتظار الاستكمال
                  style: const TextStyle(fontSize: 12, color: Color(0xFFD97706)),
                ),
              ],
            ),
            const Spacer(),
            const Icon(Icons.chevron_left, color: Color(0xFFD97706)),
          ],
        ),
      ),
    );
  }

  Widget _buildSummaryCard({
    required String title,
    required int count,
    required IconData icon,
    required double lyd,
    required double usd,
    required double eur,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(20),
      child: Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.grey.shade100, width: 2),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.02),
              blurRadius: 10,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.blue.shade50,
                    shape: BoxShape.circle,
                  ),
                  child: Icon(icon, color: Colors.blue.shade700, size: 20),
                ),
                Column(
                  children: [
                    Text(
                      title,
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF1E293B)),
                    ),
                    Text(
                      '$count \u0639\u0645\u0644\u064a\u0629',
                      style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
                    ),
                  ],
                ),
                const SizedBox(width: 40),
              ],
            ),
            const SizedBox(height: 24),
            _buildCurrencyRow('LYD', lyd),
            _buildCurrencyRow('USD', usd),
            _buildCurrencyRow('EUR', eur),
          ],
        ),
      ),
    );
  }

  Widget _buildCurrencyRow(String currency, double amount) {
    final format = NumberFormat.currency(symbol: '', decimalDigits: 0);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Directionality(
        textDirection: TextDirection.ltr,
        child: Row(
          children: [
            Text(
              currency,
              style: const TextStyle(color: Color(0xFF94A3B8), fontWeight: FontWeight.bold, fontSize: 13),
            ),
            const Spacer(),
            Text(
              format.format(amount),
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w900, color: Color(0xFF1E293B)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSubscriptionAlertCard(SubscriptionStatus status, BuildContext context) {
    final isExpired = status.isExpired;
    final color = isExpired ? const Color(0xFFE11D48) : const Color(0xFFD97706);
    final bgColor = isExpired ? const Color(0xFFFFF1F2) : const Color(0xFFFFFBEB);
    final borderColor = isExpired ? const Color(0xFFFECDD3) : const Color(0xFFFDE68A);

    String title = isExpired ? 'انتهى اشتراكك (وضع استعراض البيانات فقط)' : 'أوشك اشتراكك على الانتهاء';
    String message = isExpired
        ? 'يمكنك تصفح عملياتك وتصديرها كـ PDF و Excel. لتفعيل المراقبة ومعالجة الرسائل، يرجى تجديد الاشتراك.'
        : 'متبقٍ ${status.daysRemaining} يوماً في خطتك (${status.planType}). جدّد اشتراكك الآن لتفادي انقطاع الخدمة.';

    return InkWell(
      onTap: () => context.push('/subscriptions').then((_) => _loadSubscriptionStatus()),
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: borderColor),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(isExpired ? Icons.lock_clock : Icons.warning_amber_rounded, color: color, size: 24),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: color),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    message,
                    style: TextStyle(fontSize: 12, color: color.withOpacity(0.9), height: 1.4),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: color,
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Text(
                'تجديد',
                style: TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
