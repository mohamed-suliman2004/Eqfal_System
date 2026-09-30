import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' hide TextDirection;
import 'package:go_router/go_router.dart' as import_go_router;
import '../providers/operations_provider.dart';
import '../models/operation.dart';
import '../services/export_service.dart';
import 'drafts_screen.dart';
import '../widgets/page_help_dialog.dart';

class OperationsListScreen extends ConsumerStatefulWidget {
  final String category;

  const OperationsListScreen({super.key, required this.category});

  @override
  ConsumerState<OperationsListScreen> createState() => _OperationsListScreenState();
}

class _OperationsListScreenState extends ConsumerState<OperationsListScreen> {
  String get _normalizedCategory {
    String cat = widget.category;
    try {
      cat = Uri.decodeComponent(cat);
    } catch (_) {}
    cat = cat.trim();

    if (cat == 'تسليم' || cat.toLowerCase() == 'delivery') {
      return 'تسليم';
    }
    if (cat == 'استلام' || cat.toLowerCase() == 'receipt') {
      return 'استلام';
    }
    return cat;
  }

  // 0: الكل, 1: غير مراجعة (افتراضي), 2: مراجعة, 3: محذوفة
  int _selectedTab = 1;
  DateTime? _startDate;
  DateTime? _endDate;
  bool _isSearching = false;
  final TextEditingController _searchController = TextEditingController();

  // Optimistic reviewed state tracker (Locks user actions immediately)
  final Map<int, bool> _reviewedOverrides = {};
  // IDs of items currently animating out of the tab
  final Set<int> _animatingOps = {};

  // Selection mode for batch actions
  bool _isSelectionMode = false;
  final Set<int> _selectedIds = {};
  bool _isBatchDeleting = false;

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  bool _isOpReviewed(Operation op) {
    return _reviewedOverrides[op.id] ?? op.isReviewed;
  }

  void _toggleSelection(int id) {
    setState(() {
      if (_selectedIds.contains(id)) {
        _selectedIds.remove(id);
        if (_selectedIds.isEmpty) {
          _isSelectionMode = false;
        }
      } else {
        _selectedIds.add(id);
      }
    });
  }

  void _toggleSelectAll(List<Operation> list) {
    setState(() {
      if (_selectedIds.length == list.length) {
        _selectedIds.clear();
      } else {
        _selectedIds.addAll(list.map((o) => o.id));
      }
    });
  }

  Future<void> _restoreOp(Operation op) async {
    try {
      final success = await ref.read(apiServiceProvider).restoreOperation(op.id);
      if (success) {
        ref.invalidate(operationsProvider);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Row(
                children: [
                  Icon(Icons.check_circle, color: Colors.white),
                  SizedBox(width: 8),
                  Text('تمت استعادة العملية بنجاح إلى الحسابات'),
                ],
              ),
              backgroundColor: Color(0xFF10B981),
              behavior: SnackBarBehavior.floating,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(10))),
            ),
          );
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('خطأ أثناء الاستعادة: $e')),
        );
      }
    }
  }

  Future<void> _permanentDeleteOp(Operation op) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('حذف العملية', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17)),
        content: const Text('هل أنت متأكد من حذف هذه العملية؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.red,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            ),
            child: const Text('حذف'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      await ref.read(apiServiceProvider).deleteOperation(op.id);
      ref.invalidate(operationsProvider);
    }
  }

  Future<void> _confirmBatchDelete(int count) async {
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(color: Colors.red.shade50, shape: BoxShape.circle),
              child: const Icon(Icons.delete_outline, color: Colors.red, size: 24),
            ),
            const SizedBox(width: 12),
            const Text('تأكيد الحذف الجماعي', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17)),
          ],
        ),
        content: Text('هل أنت متأكد من حذف $count عملية محددة نهائياً؟ لا يمكن التراجع عن هذا الإجراء.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء', style: TextStyle(color: Colors.grey))),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.red,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            ),
            child: const Text('حذف نهائي', style: TextStyle(fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      setState(() => _isBatchDeleting = true);
      try {
        final ids = _selectedIds.toList();
        final success = await ref.read(apiServiceProvider).batchDeleteOperations(ids);
        if (success) {
          ref.invalidate(operationsProvider);
          if (mounted) {
            messenger.showSnackBar(
              SnackBar(
                content: Text('تم حذف $count عملية بنجاح'),
                backgroundColor: const Color(0xFF10B981),
                behavior: SnackBarBehavior.floating,
                shape: const RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(10))),
              ),
            );
          }
        }
      } catch (e) {
        if (mounted) {
          messenger.showSnackBar(SnackBar(content: Text('خطأ: $e')));
        }
      } finally {
        if (mounted) {
          setState(() {
            _selectedIds.clear();
            _isSelectionMode = false;
            _isBatchDeleting = false;
          });
        }
      }
    }
  }

  Future<void> _showFilterSheet() async {
    await showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setModalState) {
            return Padding(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('تصفية بالتاريخ', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 24),
                  const Text('نطاق التاريخ', style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF64748B))),
                  const SizedBox(height: 8),
                  InkWell(
                    onTap: () async {
                      final range = await showDateRangePicker(
                        context: context,
                        firstDate: DateTime(2020),
                        lastDate: DateTime.now().add(const Duration(days: 365)),
                        initialDateRange: _startDate != null && _endDate != null
                            ? DateTimeRange(start: _startDate!, end: _endDate!)
                            : null,
                      );
                      if (range != null) {
                        setModalState(() {
                          _startDate = range.start;
                          _endDate = range.end;
                        });
                        setState(() {});
                      }
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                      decoration: BoxDecoration(
                        border: Border.all(color: Colors.grey.shade300),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            _startDate != null && _endDate != null
                                ? '${DateFormat('yyyy-MM-dd').format(_startDate!)} إلى ${DateFormat('yyyy-MM-dd').format(_endDate!)}'
                                : 'اختر الفترة',
                            style: TextStyle(color: _startDate != null ? Colors.black : Colors.grey.shade600),
                          ),
                          const Icon(Icons.calendar_today, size: 20, color: Colors.grey),
                        ],
                      ),
                    ),
                  ),
                  if (_startDate != null) ...[
                    const SizedBox(height: 12),
                    OutlinedButton(
                      onPressed: () {
                        setModalState(() {
                          _startDate = null;
                          _endDate = null;
                        });
                        setState(() {});
                      },
                      style: OutlinedButton.styleFrom(foregroundColor: Colors.red, side: const BorderSide(color: Colors.red), shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8))),
                      child: const Text('إلغاء'),
                    )
                  ]
                ],
              ),
            );
          },
        );
      },
    );
  }

  Future<void> _showExportSheet(List<Operation> filteredOps) async {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('تصدير البيانات', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 8),
              Text('تصدير ${filteredOps.length} عملية', style: const TextStyle(color: Colors.grey)),
              const SizedBox(height: 24),
              ListTile(
                leading: const Icon(Icons.table_chart, color: Colors.green),
                title: const Text('تصدير إلى إكسل (Excel)'),
                onTap: () {
                  Navigator.pop(context);
                  ExportService.exportToExcel(filteredOps, _normalizedCategory);
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('جاري تصدير الملف...')));
                },
              ),
              const Divider(),
              ListTile(
                leading: const Icon(Icons.picture_as_pdf, color: Colors.red),
                title: const Text('تصدير إلى PDF'),
                onTap: () {
                  Navigator.pop(context);
                  ExportService.exportToPdf(filteredOps, _normalizedCategory);
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('جاري تصدير PDF...')));
                },
              ),
            ],
          ),
        );
      },
    );
  }

  List<Operation> _getBaseOps(List<Operation> allOperations) {
    return allOperations.where((op) {
      final opCat = (op.category ?? '').trim();
      final targetCat = _normalizedCategory.trim();
      // Exclude deleted operations from regular tabs
      if (op.isDeleted) return false;
      // Use shared isDraft logic (includes party empty check)
      return opCat == targetCat && !DraftsScreen.isDraft(op);
    }).toList();
  }

  List<Operation> _applySearchAndDateFilter(List<Operation> ops, AsyncValue<List<dynamic>> keywordsAsync) {
    var result = ops;

    if (_startDate != null && _endDate != null) {
      result = result.where((op) {
        return op.createdAt.isAfter(_startDate!.subtract(const Duration(days: 1))) &&
            op.createdAt.isBefore(_endDate!.add(const Duration(days: 1)));
      }).toList();
    }

    if (_searchController.text.isNotEmpty) {
      final dynamicCurrencyMap = <String, List<String>>{};
      if (keywordsAsync.hasValue && keywordsAsync.value != null) {
        for (final kw in keywordsAsync.value!) {
          if (kw.type != 'تسليم' && kw.type != 'استلام') {
            final code = kw.type.toLowerCase();
            if (!dynamicCurrencyMap.containsKey(code)) {
              dynamicCurrencyMap[code] = [];
            }
            dynamicCurrencyMap[code]!.add(kw.word.toLowerCase());
          }
        }
      }

      final query = _searchController.text.toLowerCase();
      result = result.where((op) {
        final matchAmount = op.amount?.toString().contains(query) ?? false;
        final currency = op.currency?.toLowerCase() ?? '';
        bool matchCurrency = currency.contains(query);
        if (!matchCurrency) {
          final aliases = dynamicCurrencyMap[currency];
          if (aliases != null) {
            for (final alias in aliases) {
              if (query.contains(alias) || alias.contains(query)) {
                matchCurrency = true;
                break;
              }
            }
          }
        }
        final matchParty = op.party?.toLowerCase().contains(query) ?? false;
        final matchNotes = op.notes?.toLowerCase().contains(query) ?? false;
        return matchAmount || matchCurrency || matchParty || matchNotes;
      }).toList();
    }

    return result;
  }

  Future<void> _handleCheckboxToggle(Operation op, bool newValue) async {
    // 1. Optimistic instant local update
    setState(() {
      _reviewedOverrides[op.id] = newValue;
      if (_selectedTab == 1 || _selectedTab == 2) {
        _animatingOps.add(op.id);
      }
    });

    final apiService = ref.read(apiServiceProvider);

    // 2. Perform backend update
    apiService.updateOperation(op.copyWith(isReviewed: newValue));

    // 3. Smooth visual delay before removing card from filtered tabs
    if (_selectedTab == 1 || _selectedTab == 2) {
      await Future.delayed(const Duration(milliseconds: 350));
    }

    if (mounted) {
      setState(() {
        _animatingOps.remove(op.id);
      });
      ref.invalidate(operationsProvider);
    }
  }

  @override
  Widget build(BuildContext context) {
    final asyncValue = ref.watch(operationsProvider);
    final keywordsAsync = ref.watch(keywordsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: !_isSearching,
        iconTheme: const IconThemeData(color: Colors.black87),
        leading: _isSelectionMode
            ? IconButton(
                icon: const Icon(Icons.close),
                onPressed: () {
                  setState(() {
                    _isSelectionMode = false;
                    _selectedIds.clear();
                  });
                },
              )
            : (_isSearching ? const SizedBox() : null),
        leadingWidth: _isSelectionMode ? 56 : (_isSearching ? 0 : 56),
        title: _isSelectionMode
            ? Text(
                'تم تحديد (${_selectedIds.length})',
                style: const TextStyle(color: Color(0xFF1E293B), fontWeight: FontWeight.bold, fontSize: 17),
              )
            : (_isSearching
                ? TextField(
                    controller: _searchController,
                    autofocus: true,
                    onChanged: (value) => setState(() {}),
                    decoration: InputDecoration(
                      hintText: 'بحث بالمبلغ أو الطرف...',
                      hintStyle: TextStyle(color: Colors.grey.shade400, fontSize: 14),
                      border: InputBorder.none,
                    ),
                  )
                : asyncValue.when(
                    data: (ops) {
                      final baseOps = _getBaseOps(ops);
                      return Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            '$_normalizedCategory (${baseOps.length})',
                            style: const TextStyle(color: Colors.black87, fontWeight: FontWeight.bold, fontSize: 18),
                          ),
                          const SizedBox(width: 8),
                          Icon(
                            _normalizedCategory == 'استلام' ? Icons.arrow_downward : Icons.arrow_upward,
                            size: 20,
                            color: Colors.black87,
                          ),
                        ],
                      );
                    },
                    loading: () => Text(_normalizedCategory, style: const TextStyle(color: Colors.black87)),
                    error: (_, _) => Text(_normalizedCategory, style: const TextStyle(color: Colors.black87)),
                  )),
        actions: _isSelectionMode
            ? [
                asyncValue.maybeWhen(
                  data: (ops) {
                    final currentList = _getCurrentDisplayedList(ops, keywordsAsync);
                    final allSelected = currentList.isNotEmpty && _selectedIds.length == currentList.length;
                    return TextButton.icon(
                      onPressed: () => _toggleSelectAll(currentList),
                      icon: Icon(allSelected ? Icons.deselect : Icons.select_all, size: 20, color: const Color(0xFF2563EB)),
                      label: Text(allSelected ? 'إلغاء الكل' : 'تحديد الكل', style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF2563EB))),
                    );
                  },
                  orElse: () => const SizedBox(),
                ),
              ]
            : (_isSearching
                ? [
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () {
                        setState(() {
                          _isSearching = false;
                          _searchController.clear();
                        });
                      },
                    )
                  ]
                : [
                    IconButton(
                      icon: const Icon(Icons.search),
                      onPressed: () => setState(() => _isSearching = true),
                    ),
                    IconButton(
                      icon: const Icon(Icons.checklist_rtl_rounded),
                      tooltip: 'تحديد متعدد',
                      onPressed: () => setState(() => _isSelectionMode = true),
                    ),
                    asyncValue.when(
                      data: (ops) {
                        final baseOps = _getBaseOps(ops);
                        final filtered = _applySearchAndDateFilter(baseOps, keywordsAsync);
                        return IconButton(
                          icon: const Icon(Icons.download_outlined),
                          onPressed: () => _showExportSheet(filtered),
                        );
                      },
                      loading: () => const SizedBox(),
                      error: (_, _) => const SizedBox(),
                    ),
                    IconButton(
                      icon: const Icon(Icons.filter_list),
                      onPressed: _showFilterSheet,
                    ),
                    IconButton(
                      icon: const Icon(Icons.help_outline_rounded, color: Color(0xFF2563EB)),
                      tooltip: 'مساعدة وإرشادات',
                      onPressed: () {
                        PageHelpDialog.show(
                          context: context,
                          title: 'دليل شاشة العمليات',
                          subtitle: 'كيف تدير وتراجع عملياتك وفواتيرك؟',
                          steps: const [
                            HelpStep(
                              icon: Icons.check_circle_outline,
                              title: 'المراجعة والاعتماد',
                              description: 'اضغط على مربع الاختيار بجانب كل عملية لاعتمادها، ويمكنك التبديل بين تبويبات (الكل، بانتظار المراجعة، تمت المراجعة).',
                            ),
                            HelpStep(
                              icon: Icons.edit_note_rounded,
                              title: 'كشف الرسائل المعدلة',
                              description: 'إذا قام الطرف الآخر بتعديل رسالته في الواتساب بعد إرسالها، ستظهر علامة (معدلة) واضحة على البطاقة لحمايتك من التلاعب.',
                              highlightBadge: 'معدلة',
                            ),
                            HelpStep(
                              icon: Icons.search_rounded,
                              title: 'البحث والتصفية بالتواريخ',
                              description: 'استخدم أيقونة البحث للبحث بالمبلغ أو اسم المصرف، وأيقونة التصفية لتحديد نطاق زمني معين.',
                            ),
                            HelpStep(
                              icon: Icons.download_outlined,
                              title: 'تصدير التقارير',
                              description: 'اضغط على أيقونة التحميل لتصدير جدول العمليات بصيغة PDF أو Excel لطباعته أو مشاركته فوراً.',
                            ),
                          ],
                        );
                      },
                    ),
                  ]),
      ),
      bottomNavigationBar: (_isSelectionMode && _selectedIds.isNotEmpty)
          ? Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              decoration: BoxDecoration(
                color: Colors.white,
                boxShadow: [
                  BoxShadow(color: Colors.black.withValues(alpha: 0.08), blurRadius: 16, offset: const Offset(0, -4)),
                ],
              ),
              child: SafeArea(
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        'محدد: ${_selectedIds.length} عملية',
                        style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF475569), fontSize: 14),
                      ),
                    ),
                    ElevatedButton.icon(
                      onPressed: _isBatchDeleting ? null : () => _confirmBatchDelete(_selectedIds.length),
                      icon: _isBatchDeleting
                          ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                          : const Icon(Icons.delete_forever, size: 20),
                      label: Text(
                        _isBatchDeleting ? 'جارٍ الحذف...' : 'حذف نهائي (${_selectedIds.length})',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.red.shade600,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                        elevation: 0,
                      ),
                    ),
                  ],
                ),
              ),
            )
          : null,
      body: asyncValue.when(
        data: (ops) {
          final baseOps = _getBaseOps(ops);
          final filteredBase = _applySearchAndDateFilter(baseOps, keywordsAsync);

          // Calculate real-time counts using optimistic state
          final totalCount = filteredBase.length;
          final unreviewedCount = filteredBase.where((o) => !_isOpReviewed(o)).length;
          final reviewedCount = filteredBase.where((o) => _isOpReviewed(o)).length;

          // Deleted operations for this category
          final allDeleted = ops.where((o) => o.isDeleted && (o.category == _normalizedCategory || _normalizedCategory.isEmpty)).toList();
          final filteredDeleted = _applySearchAndDateFilter(allDeleted, keywordsAsync);
          final deletedCount = filteredDeleted.length;

          // Build current tab list
          List<Operation> displayedList;
          if (_selectedTab == 0) {
            displayedList = filteredBase;
          } else if (_selectedTab == 1) {
            displayedList = filteredBase.where((o) {
              if (_animatingOps.contains(o.id)) return true;
              return !_isOpReviewed(o);
            }).toList();
          } else if (_selectedTab == 2) {
            displayedList = filteredBase.where((o) {
              if (_animatingOps.contains(o.id)) return true;
              return _isOpReviewed(o);
            }).toList();
          } else {
            displayedList = filteredDeleted;
          }

          return Column(
            children: [
              // Tabs with dynamic counts: الكل / غير مراجعة / مراجعة / محذوفة
              Container(
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(
                  color: Colors.grey.shade100,
                  borderRadius: BorderRadius.circular(24),
                ),
                child: Row(
                  children: [
                    _buildTab(0, 'الكل ($totalCount)'),
                    _buildTab(1, 'غير مراجعة ($unreviewedCount)'),
                    _buildTab(2, 'مراجعة ($reviewedCount)'),
                    _buildTab(3, 'محذوفة ($deletedCount)'),
                  ],
                ),
              ),
              Expanded(
                child: displayedList.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              _selectedTab == 3 ? Icons.delete_outline : Icons.inbox_outlined,
                              size: 64,
                              color: Colors.grey.shade300,
                            ),
                            const SizedBox(height: 16),
                            Text(
                              _selectedTab == 1
                                  ? 'لا توجد عمليات غير مراجعة'
                                  : (_selectedTab == 2
                                      ? 'لا توجد عمليات مراجعة'
                                      : (_selectedTab == 3
                                          ? 'لا توجد عمليات محذوفة من الواتساب'
                                          : 'لا توجد عمليات')),
                              style: TextStyle(color: Colors.grey.shade400, fontSize: 16),
                            ),
                          ],
                        ),
                      )
                    : ListView.builder(
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                        itemCount: displayedList.length,
                        itemBuilder: (context, index) {
                          final op = displayedList[index];
                          final isSelected = _selectedIds.contains(op.id);
                          final isDeletedTab = _selectedTab == 3;

                          if (isDeletedTab) {
                            return _buildDeletedCard(op, isSelected);
                          }

                          return _buildStandardCard(op, isSelected);
                        },
                      ),
              ),
            ],
          );
        },
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, stack) => Center(child: Text('خطأ: $error')),
      ),
    );
  }

  List<Operation> _getCurrentDisplayedList(List<Operation> ops, AsyncValue<List<dynamic>> keywordsAsync) {
    if (_selectedTab == 3) {
      final allDeleted = ops.where((o) => o.isDeleted && (o.category == _normalizedCategory || _normalizedCategory.isEmpty)).toList();
      return _applySearchAndDateFilter(allDeleted, keywordsAsync);
    }
    final baseOps = _getBaseOps(ops);
    final filteredBase = _applySearchAndDateFilter(baseOps, keywordsAsync);
    if (_selectedTab == 0) return filteredBase;
    if (_selectedTab == 1) return filteredBase.where((o) => !_isOpReviewed(o)).toList();
    return filteredBase.where((o) => _isOpReviewed(o)).toList();
  }

  Widget _buildDeletedCard(Operation op, bool isSelected) {
    final amount = op.amount ?? 0;
    final currency = op.currency ?? '';
    final party = op.party ?? 'غير محدد';
    final time = DateFormat('hh:mm a').format(op.createdAt).replaceAll('AM', 'ص').replaceAll('PM', 'م');
    final format = NumberFormat.currency(symbol: '', decimalDigits: 0);

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: isSelected ? const Color(0xFFFEF2F2) : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: isSelected ? Colors.red.shade400 : const Color(0xFFFECACA),
          width: isSelected ? 1.8 : 1.2,
        ),
        boxShadow: [
          BoxShadow(color: Colors.red.withValues(alpha: 0.04), blurRadius: 6, offset: const Offset(0, 2)),
        ],
      ),
      child: InkWell(
        onTap: () {
          if (_isSelectionMode) {
            _toggleSelection(op.id);
          } else {
            import_go_router.GoRouter.of(context).push('/operations/${op.id}');
          }
        },
        onLongPress: () {
          if (!_isSelectionMode) {
            setState(() {
              _isSelectionMode = true;
              _selectedIds.add(op.id);
            });
          } else {
            _toggleSelection(op.id);
          }
        },
        borderRadius: BorderRadius.circular(14),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  if (_isSelectionMode) ...[
                    Transform.scale(
                      scale: 1.1,
                      child: Checkbox(
                        value: isSelected,
                        activeColor: Colors.red.shade600,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
                        onChanged: (_) => _toggleSelection(op.id),
                      ),
                    ),
                    const SizedBox(width: 4),
                  ],
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFEF2F2),
                      borderRadius: BorderRadius.circular(6),
                      border: Border.all(color: const Color(0xFFFCA5A5)),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.delete_sweep_outlined, size: 14, color: Color(0xFFDC2626)),
                        SizedBox(width: 4),
                        Text(
                          'تم الحذف من الواتساب',
                          style: TextStyle(color: Color(0xFFDC2626), fontSize: 11, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  ),
                  const Spacer(),
                  Text(time, style: const TextStyle(color: Colors.grey, fontSize: 12)),
                ],
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Directionality(
                    textDirection: TextDirection.ltr,
                    child: Text(
                      '${format.format(amount)} $currency',
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 17,
                        color: Color(0xFF1E293B),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      party,
                      style: const TextStyle(color: Color(0xFF64748B), fontSize: 13, fontWeight: FontWeight.w600),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
              if (op.cleanRawMessage.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(
                  '«${op.cleanRawMessage}»',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
                ),
              ],
              const Divider(height: 18),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton.icon(
                    onPressed: () => _permanentDeleteOp(op),
                    icon: const Icon(Icons.delete_outline, size: 17, color: Colors.red),
                    label: const Text('حذف', style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold, fontSize: 12)),
                    style: TextButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                      backgroundColor: const Color(0xFFFEF2F2),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStandardCard(Operation op, bool isSelected) {
    final amount = op.amount ?? 0;
    final currency = op.currency ?? '';
    final party = op.party ?? 'غير محدد';
    final time = DateFormat('hh:mm a').format(op.createdAt).replaceAll('AM', 'ص').replaceAll('PM', 'م');
    final format = NumberFormat.currency(symbol: '', decimalDigits: 0);

    final bool isReviewed = _isOpReviewed(op);
    final bool isAnimating = _animatingOps.contains(op.id);
    final bool shouldFadeOut = isAnimating && (_selectedTab == 1 || _selectedTab == 2);

    return AnimatedOpacity(
      opacity: shouldFadeOut ? 0.0 : 1.0,
      duration: const Duration(milliseconds: 350),
      curve: Curves.easeInOut,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 300),
        margin: const EdgeInsets.only(bottom: 8),
        decoration: BoxDecoration(
          color: isSelected
              ? const Color(0xFFEFF6FF)
              : (isAnimating ? Colors.green.shade50 : (isReviewed ? const Color(0xFFF8FAFC) : Colors.white)),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: isSelected
                ? const Color(0xFF3B82F6)
                : (isAnimating ? Colors.green.shade300 : (isReviewed ? Colors.grey.shade200 : Colors.grey.shade100)),
            width: isSelected ? 1.6 : (isAnimating ? 1.5 : 1.0),
          ),
          boxShadow: [
            BoxShadow(color: Colors.black.withValues(alpha: 0.02), blurRadius: 4, offset: const Offset(0, 2)),
          ],
        ),
        child: IntrinsicHeight(
          child: Row(
            children: [
              Container(
                width: 4,
                decoration: BoxDecoration(
                  color: isSelected ? Colors.blue : (isReviewed ? Colors.blue : (isAnimating ? Colors.green : Colors.transparent)),
                  borderRadius: const BorderRadius.horizontal(right: Radius.circular(12)),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 6),
                child: Transform.scale(
                  scale: 1.1,
                  child: Checkbox(
                    value: _isSelectionMode ? isSelected : isReviewed,
                    activeColor: const Color(0xFF2563EB),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
                    side: BorderSide(
                      color: (_isSelectionMode ? isSelected : isReviewed) ? const Color(0xFF2563EB) : Colors.grey.shade400,
                      width: 1.5,
                    ),
                    onChanged: (v) {
                      if (_isSelectionMode) {
                        _toggleSelection(op.id);
                      } else if (v != null) {
                        _handleCheckboxToggle(op, v);
                      }
                    },
                  ),
                ),
              ),
              Expanded(
                child: InkWell(
                  onTap: () {
                    if (_isSelectionMode) {
                      _toggleSelection(op.id);
                    } else {
                      import_go_router.GoRouter.of(context).push('/operations/${op.id}');
                    }
                  },
                  onLongPress: () {
                    if (!_isSelectionMode) {
                      setState(() {
                        _isSelectionMode = true;
                        _selectedIds.add(op.id);
                      });
                    } else {
                      _toggleSelection(op.id);
                    }
                  },
                  borderRadius: const BorderRadius.horizontal(left: Radius.circular(12)),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 8),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(
                              op.isOutgoing ? Icons.arrow_upward : Icons.arrow_downward,
                              color: op.isOutgoing ? Colors.green : Colors.grey,
                              size: 16,
                            ),
                            const SizedBox(width: 4),
                            Directionality(
                              textDirection: TextDirection.ltr,
                              child: Text(
                                '${format.format(amount)} $currency',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 16,
                                  color: isReviewed ? Colors.grey.shade500 : const Color(0xFF1E293B),
                                  decoration: isReviewed ? TextDecoration.lineThrough : null,
                                ),
                              ),
                            ),
                            const Spacer(),
                            if (_selectedTab == 0 && isReviewed) ...[
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                decoration: BoxDecoration(
                                  color: const Color(0xFFEFF6FF),
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: const Text(
                                  'مراجعة',
                                  style: TextStyle(color: Color(0xFF2563EB), fontSize: 10, fontWeight: FontWeight.bold),
                                ),
                              ),
                              const SizedBox(width: 6),
                            ],
                            Text(
                              time,
                              style: const TextStyle(color: Colors.grey, fontSize: 12),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          party,
                          style: TextStyle(
                            color: isReviewed ? Colors.grey.shade400 : const Color(0xFF64748B),
                            fontSize: 13,
                          ),
                        ),
                        if (op.isEdited) ...[
                          const SizedBox(height: 6),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                            decoration: BoxDecoration(
                              color: const Color(0xFFEFF6FF),
                              borderRadius: BorderRadius.circular(6),
                              border: Border.all(color: const Color(0xFFBFDBFE)),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Icon(Icons.edit_note_rounded, size: 14, color: Color(0xFF2563EB)),
                                const SizedBox(width: 4),
                                Flexible(
                                  child: Text(
                                    op.notes ?? 'تم تعديل الرسالة في الواتساب',
                                    style: const TextStyle(
                                      color: Color(0xFF1D4ED8),
                                      fontSize: 11,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTab(int index, String title) {
    final isSelected = _selectedTab == index;
    return Expanded(
      child: GestureDetector(
        onTap: () {
          setState(() {
            _selectedTab = index;
            _selectedIds.clear();
          });
        },
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 8),
          decoration: BoxDecoration(
            color: isSelected ? Colors.white : Colors.transparent,
            borderRadius: BorderRadius.circular(20),
            border: isSelected ? Border.all(color: index == 3 ? Colors.red.shade100 : Colors.blue.shade100) : null,
            boxShadow: isSelected
                ? [BoxShadow(color: Colors.black.withValues(alpha: 0.05), blurRadius: 4, offset: const Offset(0, 2))]
                : null,
          ),
          alignment: Alignment.center,
          child: Text(
            title,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: isSelected
                  ? (index == 3 ? Colors.red.shade700 : Colors.blue.shade700)
                  : Colors.grey.shade600,
              fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
              fontSize: 11,
            ),
          ),
        ),
      ),
    );
  }
}
