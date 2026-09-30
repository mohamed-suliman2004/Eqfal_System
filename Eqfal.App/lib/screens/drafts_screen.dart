import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../providers/operations_provider.dart';
import '../models/operation.dart';
import 'package:go_router/go_router.dart';

class DraftsScreen extends ConsumerStatefulWidget {
  const DraftsScreen({super.key});

  // Shared draft-check logic - MUST match dashboard_screen.dart and operations_list_screen.dart exactly
  static bool isDraft(Operation op) {
    if (op.isDeleted) return false;
    final status = op.status.trim();
    if (status == 'مكتمل' || status == 'completed') {
      // Even if status is "مكتمل", treat as draft if party is missing
      final party = (op.party ?? '').trim();
      if (party.isEmpty) return true;
      return false;
    }
    final curr = (op.currency ?? '').trim();
    final cat = (op.category ?? '').trim();
    final party = (op.party ?? '').trim();
    return status == 'مسودة' ||
        status == 'draft' ||
        status == 'ناقص' ||
        status == 'غير مكتمل' ||
        curr.isEmpty ||
        curr == 'غير محدد' ||
        cat.isEmpty ||
        cat == 'غير محدد' ||
        party.isEmpty;
  }

  @override
  ConsumerState<DraftsScreen> createState() => _DraftsScreenState();
}

class _DraftsScreenState extends ConsumerState<DraftsScreen> {
  bool _isSelectionMode = false;
  final Set<int> _selectedIds = {};
  bool _isDeleting = false;

  void _toggleSelectAll(List<Operation> drafts) {
    setState(() {
      if (_selectedIds.length == drafts.length) {
        _selectedIds.clear();
      } else {
        _selectedIds.addAll(drafts.map((d) => d.id));
      }
    });
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
              decoration: BoxDecoration(
                color: Colors.red.shade50,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.delete_outline, color: Colors.red, size: 24),
            ),
            const SizedBox(width: 12),
            const Text(
              'تأكيد الحذف الجماعي',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17),
            ),
          ],
        ),
        content: Text(
          'هل أنت متأكد من حذف $count مسودة محددة؟ لا يمكن التراجع عن هذا الإجراء.',
          style: const TextStyle(fontSize: 14, color: Color(0xFF475569), height: 1.4),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey, fontWeight: FontWeight.bold)),
          ),
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
      setState(() => _isDeleting = true);
      try {
        final idsToDelete = _selectedIds.toList();
        final success = await ref.read(apiServiceProvider).batchDeleteOperations(idsToDelete);
        if (success) {
          ref.invalidate(operationsProvider);
          if (mounted) {
            messenger.showSnackBar(
              SnackBar(
                content: Row(
                  children: [
                    const Icon(Icons.check_circle, color: Colors.white),
                    const SizedBox(width: 8),
                    Text('تم حذف $count مسودة بنجاح'),
                  ],
                ),
                backgroundColor: const Color(0xFF10B981),
                behavior: SnackBarBehavior.floating,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            );
          }
        } else {
          if (mounted) {
            messenger.showSnackBar(
              const SnackBar(content: Text('حدث خطأ أثناء محاولة الحذف')),
            );
          }
        }
      } catch (e) {
        if (mounted) {
          messenger.showSnackBar(
            SnackBar(content: Text('خطأ: $e')),
          );
        }
      } finally {
        if (mounted) {
          setState(() {
            _selectedIds.clear();
            _isSelectionMode = false;
            _isDeleting = false;
          });
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final asyncValue = ref.watch(operationsProvider);

    // Extract the last known data (even if currently loading/refreshing)
    final List<Operation> allOps = asyncValue.asData?.value ?? const [];
    final List<Operation> drafts = allOps.where(DraftsScreen.isDraft).toList();
    final bool isLoading = asyncValue.isLoading && allOps.isEmpty;
    final bool hasError = asyncValue.hasError && allOps.isEmpty;

    final bool allSelected = drafts.isNotEmpty && _selectedIds.length == drafts.length;

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
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
                tooltip: 'إلغاء التحديد',
              )
            : null,
        title: _isSelectionMode
            ? Text(
                'تم تحديد (${_selectedIds.length})',
                style: const TextStyle(
                  color: Color(0xFF1E293B),
                  fontWeight: FontWeight.bold,
                  fontSize: 17,
                ),
              )
            : Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'المسودات (${drafts.length})',
                    style: const TextStyle(
                      color: Colors.black87,
                      fontWeight: FontWeight.bold,
                      fontSize: 18,
                    ),
                  ),
                  const SizedBox(width: 8),
                  const Icon(
                    Icons.assignment_late_outlined,
                    size: 20,
                    color: Color(0xFFEA580C),
                  ),
                ],
              ),
        actions: [
          if (drafts.isNotEmpty) ...[
            if (_isSelectionMode)
              TextButton.icon(
                onPressed: () => _toggleSelectAll(drafts),
                icon: Icon(
                  allSelected ? Icons.deselect : Icons.select_all,
                  size: 20,
                  color: const Color(0xFF2563EB),
                ),
                label: Text(
                  allSelected ? 'إلغاء الكل' : 'تحديد الكل',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF2563EB),
                  ),
                ),
              )
            else
              IconButton(
                icon: const Icon(Icons.checklist_rtl_rounded, color: Color(0xFF475569)),
                onPressed: () {
                  setState(() {
                    _isSelectionMode = true;
                  });
                },
                tooltip: 'تحديد متعدد للحذف',
              ),
          ],
        ],
      ),
      bottomNavigationBar: (_isSelectionMode && _selectedIds.isNotEmpty)
          ? Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              decoration: BoxDecoration(
                color: Colors.white,
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
                    blurRadius: 16,
                    offset: const Offset(0, -4),
                  ),
                ],
              ),
              child: SafeArea(
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        'محدد: ${_selectedIds.length} من أصل ${drafts.length}',
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF475569),
                          fontSize: 14,
                        ),
                      ),
                    ),
                    ElevatedButton.icon(
                      onPressed: _isDeleting ? null : () => _confirmBatchDelete(_selectedIds.length),
                      icon: _isDeleting
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Icon(Icons.delete_outline, size: 20),
                      label: Text(
                        _isDeleting ? 'جارٍ الحذف...' : 'حذف المحدد (${_selectedIds.length})',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.red.shade600,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                        elevation: 0,
                      ),
                    ),
                  ],
                ),
              ),
            )
          : null,
      body: isLoading
          ? const Center(child: CircularProgressIndicator())
          : hasError
              ? const Center(child: Text('حدث خطأ أثناء جلب المسودات'))
              : drafts.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(Icons.check_circle_outline, size: 64, color: Colors.green.shade300),
                          const SizedBox(height: 16),
                          const Text(
                            'رائع! لا توجد مسودات معلقة',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF334155),
                            ),
                          ),
                          const SizedBox(height: 8),
                          const Text(
                            'جميع العمليات مكتملة البيانات ومصنفة بنجاح',
                            style: TextStyle(fontSize: 14, color: Color(0xFF94A3B8)),
                          ),
                        ],
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                      itemCount: drafts.length,
                      itemBuilder: (context, index) {
                        final op = drafts[index];
                        final bool isSelected = _selectedIds.contains(op.id);
                        return _buildDraftCard(context, op, isSelected);
                      },
                    ),
    );
  }

  Widget _buildDraftCard(BuildContext context, Operation op, bool isSelected) {
    final formattedTime = DateFormat('hh:mm a').format(op.createdAt)
        .replaceAll('AM', 'ص').replaceAll('PM', 'م');
    final formattedDate = DateFormat('yyyy-MM-dd').format(op.createdAt);

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: isSelected ? const Color(0xFFFEF2F2) : Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: isSelected ? Colors.red.shade400 : const Color(0xFFFED7AA),
          width: isSelected ? 1.8 : 1.2,
        ),
        boxShadow: [
          BoxShadow(
            color: isSelected ? Colors.red.withValues(alpha: 0.06) : Colors.orange.withValues(alpha: 0.04),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: InkWell(
        onTap: () {
          if (_isSelectionMode) {
            _toggleSelection(op.id);
          } else {
            context.push('/edit/${op.id}');
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
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (_isSelectionMode) ...[
                Padding(
                  padding: const EdgeInsets.only(top: 2, left: 10),
                  child: Transform.scale(
                    scale: 1.15,
                    child: Checkbox(
                      value: isSelected,
                      activeColor: Colors.red.shade600,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(5)),
                      onChanged: (_) => _toggleSelection(op.id),
                    ),
                  ),
                ),
              ],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: const Color(0xFFFFF7ED),
                            borderRadius: BorderRadius.circular(20),
                            border: Border.all(color: const Color(0xFFFFEDD5)),
                          ),
                          child: const Text(
                            'بيانات ناقصة',
                            style: TextStyle(
                              color: Color(0xFFEA580C),
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        Text(
                          '$formattedTime • $formattedDate',
                          style: const TextStyle(color: Color(0xFF94A3B8), fontSize: 11),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      '«${op.cleanRawMessage.isNotEmpty ? op.cleanRawMessage : (op.rawMessage ?? "رسالة بدون نص")}»',
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Color(0xFF1E293B),
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        height: 1.4,
                      ),
                    ),
                    if (op.isEdited) ...[
                      const SizedBox(height: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: const Color(0xFFEFF6FF),
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: const Color(0xFFBFDBFE)),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.edit_note_rounded, size: 16, color: Color(0xFF2563EB)),
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
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        _buildMissingChip(
                          'المبلغ: ${op.amount != null && op.amount! > 0 ? op.amount : "غير محدد"}',
                          isMissing: op.amount == null || op.amount! <= 0,
                        ),
                        const SizedBox(width: 8),
                        _buildMissingChip(
                          'العملة: ${op.currency ?? "غير محددة"}',
                          isMissing: op.currency == null || op.currency!.isEmpty,
                        ),
                        const SizedBox(width: 8),
                        _buildMissingChip(
                          'التصنيف: ${op.category ?? "غير محدد"}',
                          isMissing: op.category == null || op.category!.isEmpty,
                        ),
                      ],
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

  Widget _buildMissingChip(String label, {required bool isMissing}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: isMissing ? const Color(0xFFFEF2F2) : const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(
          color: isMissing ? const Color(0xFFFECACA) : const Color(0xFFE2E8F0),
        ),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: isMissing ? const Color(0xFFDC2626) : const Color(0xFF64748B),
          fontSize: 11,
          fontWeight: isMissing ? FontWeight.bold : FontWeight.normal,
        ),
      ),
    );
  }
}
