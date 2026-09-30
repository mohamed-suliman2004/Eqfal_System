import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../providers/monitored_numbers_provider.dart';
import '../widgets/page_help_dialog.dart';

class MonitoredNumbersScreen extends ConsumerStatefulWidget {
  const MonitoredNumbersScreen({super.key});

  @override
  ConsumerState<MonitoredNumbersScreen> createState() => _MonitoredNumbersScreenState();
}

class _MonitoredNumbersScreenState extends ConsumerState<MonitoredNumbersScreen> {
  final TextEditingController _searchCtrl = TextEditingController();
  String _searchQuery = '';
  Timer? _syncTimer;

  @override
  void initState() {
    super.initState();
    Future.microtask(() {
      ref.read(monitoredNumbersProvider.notifier).fetchNumbers();
    });

    _syncTimer = Timer.periodic(const Duration(seconds: 4), (_) {
      if (mounted) {
        ref.read(monitoredNumbersProvider.notifier).fetchNumbers();
      }
    });
  }

  @override
  void dispose() {
    _syncTimer?.cancel();
    _searchCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(monitoredNumbersProvider);

    final filteredList = state.numbers.where((item) {
      if (_searchQuery.isEmpty) return true;
      final q = _searchQuery.toLowerCase();
      final name = item.contactName.toLowerCase();
      final phone = item.phoneNumber.toLowerCase();
      return name.contains(q) || phone.contains(q);
    }).toList();

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: const Color(0xFFF8FAFC),
        appBar: AppBar(
          title: const Text(
            'الأرقام المراقبة',
            style: TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 18,
              color: Color(0xFF1E293B),
            ),
          ),
          centerTitle: true,
          backgroundColor: Colors.white,
          elevation: 0.5,
          leading: IconButton(
            icon: const Icon(Icons.arrow_back, color: Color(0xFF1E293B)),
            onPressed: () {
              if (context.canPop()) {
                context.pop();
              } else {
                context.go('/dashboard');
              }
            },
          ),
          actions: [
            IconButton(
              icon: const Icon(Icons.help_outline_rounded, color: Color(0xFF2563EB)),
              tooltip: 'مساعدة وإرشادات',
              onPressed: () {
                PageHelpDialog.show(
                  context: context,
                  title: 'دليل الأرقام المراقبة',
                  subtitle: 'كيف تنضم المحادثات والقروبات للمراقبة؟',
                  steps: const [
                    HelpStep(
                      icon: Icons.tag_rounded,
                      title: 'طريقة التفعيل (#إقفال)',
                      description: 'ادخل على أي محادثة زبون أو مجموعة في الواتساب وأرسل كلمة #إقفال، وستنضم فوراً لقائمة المراقبة ويبدأ معالجة رسائلها.',
                      highlightBadge: '#إقفال',
                    ),
                    HelpStep(
                      icon: Icons.toggle_on_outlined,
                      title: 'إيقاف المراقبة مؤقتاً',
                      description: 'يمكنك إيقاف قراءة الرسائل لأي رقم أو قروب عبر مفتاح التبديل بجانبه دون الحاجة لحذفه.',
                    ),
                    HelpStep(
                      icon: Icons.delete_outline_rounded,
                      title: 'حذف المحادثة من المراقبة',
                      description: 'لحذف الرقم أو المجموعة نهائياً من المراقبة، اضغط على أيقونة الحذف المجاورة للاسم.',
                    ),
                  ],
                );
              },
            ),
          ],
        ),
        body: SafeArea(
          bottom: true,
          child: state.isLoading && state.numbers.isEmpty
              ? const Center(child: CircularProgressIndicator())
              : Column(
                  children: [
                    // Search Bar
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
                      child: TextField(
                        controller: _searchCtrl,
                        textDirection: TextDirection.rtl,
                        textAlign: TextAlign.right,
                        onChanged: (val) {
                          setState(() {
                            _searchQuery = val.trim();
                          });
                        },
                        decoration: InputDecoration(
                          hintText: 'بحث بالاسم أو الرقم...',
                          hintStyle: const TextStyle(color: Color(0xFF94A3B8), fontSize: 13),
                          prefixIcon: const Icon(Icons.search, color: Color(0xFF64748B), size: 20),
                          contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                          filled: true,
                          fillColor: Colors.white,
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(color: Color(0xFF3B82F6), width: 1.5),
                          ),
                        ),
                      ),
                    ),

                    // Numbers List
                    Expanded(
                      child: filteredList.isEmpty
                          ? Center(
                              child: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(Icons.phonelink_ring_outlined, size: 54, color: Colors.grey[400]),
                                  const SizedBox(height: 12),
                                  Text(
                                    _searchQuery.isEmpty ? 'لا توجد أرقام مراقبة مضافة حالياً' : 'لم يتم العثور على نتائج للبحث',
                                    style: TextStyle(color: Colors.grey[600], fontSize: 14),
                                  ),
                                ],
                              ),
                            )
                          : ListView.separated(
                              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
                              itemCount: filteredList.length,
                              separatorBuilder: (_, __) => const SizedBox(height: 8),
                              itemBuilder: (context, index) {
                                final item = filteredList[index];
                                return Container(
                                  decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(12),
                                    border: Border.all(color: const Color(0xFFE2E8F0)),
                                    boxShadow: [
                                      BoxShadow(
                                        color: Colors.black.withOpacity(0.02),
                                        blurRadius: 4,
                                        offset: const Offset(0, 2),
                                      ),
                                    ],
                                  ),
                                  child: Padding(
                                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                                    child: Row(
                                      children: [
                                        // Phone Icon on the Right
                                        Container(
                                          padding: const EdgeInsets.all(7),
                                          decoration: BoxDecoration(
                                            color: const Color(0xFFEFF6FF),
                                            borderRadius: BorderRadius.circular(8),
                                          ),
                                          child: const Icon(Icons.phone_android, color: Color(0xFF3B82F6), size: 18),
                                        ),
                                        const SizedBox(width: 10),

                                        // Name & Phone
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment: CrossAxisAlignment.start,
                                            mainAxisSize: MainAxisSize.min,
                                            children: [
                                              Text(
                                                item.contactName.isNotEmpty ? item.contactName : 'بدون اسم',
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                                style: const TextStyle(
                                                  fontWeight: FontWeight.bold,
                                                  fontSize: 14,
                                                  color: Color(0xFF1E293B),
                                                ),
                                              ),
                                              const SizedBox(height: 2),
                                              Text(
                                                item.phoneNumber,
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                                style: const TextStyle(
                                                  color: Color(0xFF64748B),
                                                  fontSize: 12,
                                                  fontWeight: FontWeight.w500,
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),

                                        const SizedBox(width: 8),

                                        // Compact Actions on the Left
                                        Row(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            Transform.scale(
                                              scale: 0.75,
                                              child: Switch(
                                                value: item.isActive,
                                                activeColor: const Color(0xFF3B82F6),
                                                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                                                onChanged: (val) {
                                                  ref.read(monitoredNumbersProvider.notifier).toggleStatus(item.id);
                                                },
                                              ),
                                            ),
                                            InkWell(
                                              onTap: () => _showEditDialog(item),
                                              borderRadius: BorderRadius.circular(6),
                                              child: const Padding(
                                                padding: EdgeInsets.all(6),
                                                child: Icon(Icons.edit_outlined, color: Color(0xFF3B82F6), size: 18),
                                              ),
                                            ),
                                            InkWell(
                                              onTap: () => _confirmDelete(item),
                                              borderRadius: BorderRadius.circular(6),
                                              child: const Padding(
                                                padding: EdgeInsets.all(6),
                                                child: Icon(Icons.delete_outline, color: Color(0xFFEF4444), size: 18),
                                              ),
                                            ),
                                          ],
                                        ),
                                      ],
                                    ),
                                  ),
                                );
                              },
                            ),
                    ),
                  ],
                ),
        ),
      ),
    );
  }

  void _confirmDelete(MonitoredNumber item) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تأكيد الحذف', textAlign: TextAlign.right),
        content: Text('هل أنت متأكد من حذف الرقم ${item.contactName} (${item.phoneNumber}) من قائمة المراقبة؟', textAlign: TextAlign.right),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFEF4444)),
            onPressed: () {
              Navigator.pop(ctx);
              ref.read(monitoredNumbersProvider.notifier).deleteNumber(item.id);
            },
            child: const Text('حذف', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _showEditDialog(MonitoredNumber item) {
    final nameCtrl = TextEditingController(text: item.contactName);
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تعديل اسم جهة الاتصال', textAlign: TextAlign.right),
        content: TextField(
          controller: nameCtrl,
          textDirection: TextDirection.rtl,
          textAlign: TextAlign.right,
          decoration: const InputDecoration(
            labelText: 'الاسم',
            border: OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(ctx);
              ref.read(monitoredNumbersProvider.notifier).editNumber(item.id, nameCtrl.text.trim());
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    );
  }
}