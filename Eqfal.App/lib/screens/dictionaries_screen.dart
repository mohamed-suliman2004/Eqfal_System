import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../providers/operations_provider.dart';
import '../widgets/page_help_dialog.dart';

class DictionariesScreen extends ConsumerStatefulWidget {
  const DictionariesScreen({super.key});

  @override
  ConsumerState<DictionariesScreen> createState() => _DictionariesScreenState();
}

class _DictionariesScreenState extends ConsumerState<DictionariesScreen> {
  int receiveCount = 0;
  int deliverCount = 0;
  bool isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadCounts();
  }

  Future<void> _loadCounts() async {
    final apiService = ref.read(apiServiceProvider);
    final receive = await apiService.getKeywords('استلام');
    final deliver = await apiService.getKeywords('تسليم');

    if (mounted) {
      setState(() {
        receiveCount = receive.length;
        deliverCount = deliver.length;
        isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('القواميس', style: TextStyle(color: Colors.black87, fontWeight: FontWeight.bold, fontSize: 18)),
        centerTitle: true,
        backgroundColor: Colors.white,
        iconTheme: const IconThemeData(color: Colors.black87),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.help_outline_rounded, color: Color(0xFF2563EB)),
            tooltip: 'مساعدة وإرشادات',
            onPressed: () {
              PageHelpDialog.show(
                context: context,
                title: 'دليل القواميس الذكية',
                subtitle: 'ما هي وظيفة القواميس وكيف تستفيد منها؟',
                steps: const [
                  HelpStep(
                    icon: Icons.search_rounded,
                    title: 'التصنيف التلقائي للرسائل',
                    description: 'يعتمد النظام على الكلمات المفتاحية في هذه القواميس لمعرفة ما إذا كانت الرسالة عملية (استلام) أو (تسليم).',
                  ),
                  HelpStep(
                    icon: Icons.add_circle_outline_rounded,
                    title: 'تخصيص الكلمات',
                    description: 'يمكنك الدخول على أي قاموس وإضافة كلماتك ومصطلحاتك الخاصة لرفع دقة استخراج العمليات وفقاً لطريقتك في العمل.',
                  ),
                  HelpStep(
                    icon: Icons.account_balance_rounded,
                    title: 'أسماء المصارف والعملات',
                    description: 'تساعد الكلمات المفتاحية النظام في تمييز المصارف والعملات واستخراج المبالغ بدقة متناهية.',
                  ),
                ],
              );
            },
          ),
        ],
      ),
      body: isLoading
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Center(
                    child: Text(
                      'حدد الكلمات التي تدل على نوع كل عملية.',
                      style: TextStyle(color: Color(0xFF94A3B8), fontSize: 13),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  const SizedBox(height: 24),

                  _buildDictionaryCard(
                    title: 'استلام',
                    count: receiveCount,
                    onTap: () async {
                      await context.push('/keywords/استلام');
                      _loadCounts();
                    },
                  ),

                  const SizedBox(height: 16),

                  _buildDictionaryCard(
                    title: 'تسليم',
                    count: deliverCount,
                    onTap: () async {
                      await context.push('/keywords/تسليم');
                      _loadCounts();
                    },
                  ),

                  const SizedBox(height: 16),

                  _buildDictionaryCard(
                    title: 'العملات',
                    count: 0,
                    icon: Icons.payments_outlined,
                    onTap: () async {
                      await context.push('/currencies');
                    },
                  ),
                ],
              ),
            ),
    );
  }

  Widget _buildDictionaryCard({
    required String title,
    required int count,
    required VoidCallback onTap,
    IconData icon = Icons.menu_book,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.grey.shade100, width: 1.5),
          boxShadow: [
            BoxShadow(color: Colors.black.withOpacity(0.02), blurRadius: 10, offset: const Offset(0, 4)),
          ],
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFFF0F9FF),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(icon, color: const Color(0xFF3B82F6), size: 24),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: const TextStyle(color: Color(0xFF1E293B), fontWeight: FontWeight.bold, fontSize: 16),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    title == 'العملات'
                        ? 'إدارة العملات'
                        : 'عدد الكلمات: $count',
                    style: const TextStyle(color: Color(0xFF94A3B8), fontSize: 12),
                  ),
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