import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../models/operation.dart';
import '../providers/operations_provider.dart';

class OperationDetailsScreen extends ConsumerStatefulWidget {
  final String id;
  const OperationDetailsScreen({super.key, required this.id});

  @override
  ConsumerState<OperationDetailsScreen> createState() => _OperationDetailsScreenState();
}

class _OperationDetailsScreenState extends ConsumerState<OperationDetailsScreen> {
  Operation? operation;
  bool isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadOperation();
  }

  Future<void> _loadOperation() async {
    final apiService = ref.read(apiServiceProvider);
    final op = await apiService.getOperation(int.parse(widget.id));
    if (mounted) {
      setState(() {
        operation = op;
        isLoading = false;
      });
    }
  }

  String _formatDisplayNumber(String? raw) {
    if (raw == null || raw.trim().isEmpty) return 'غير محدد';
    return raw.trim();
  }

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Scaffold(
        backgroundColor: Color(0xFFF8FAFC),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (operation == null) {
      return Scaffold(
        backgroundColor: const Color(0xFFF8FAFC),
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          leading: IconButton(
            icon: const Icon(Icons.arrow_back, color: Colors.black87),
            onPressed: () => context.pop(),
          ),
        ),
        body: const Center(child: Text('العملية غير موجودة', style: TextStyle(fontSize: 16))),
      );
    }

    final format = NumberFormat("#,##0.##", "en_US");
    final amount = operation!.amount ?? 0;
    final op = operation!;

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('تفاصيل العملية', style: TextStyle(color: Colors.black87, fontWeight: FontWeight.bold, fontSize: 18)),
        centerTitle: true,
        backgroundColor: Colors.white,
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.black87),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Colors.black87),
          onPressed: () => context.pop(),
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.edit_outlined, color: Color(0xFF2563EB)),
            onPressed: () {
              context.push('/operations/${widget.id}/edit').then((_) => _loadOperation());
            },
          ),
          Padding(
            padding: const EdgeInsets.only(left: 12.0, top: 12, bottom: 12),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: (op.status == 'مكتمل' || op.status == 'معتمد') ? const Color(0xFFDCFCE7) : const Color(0xFFFEF3C7),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Center(
                child: Text(
                  op.status ?? 'مسودة',
                  style: TextStyle(
                    color: (op.status == 'مكتمل' || op.status == 'معتمد') ? const Color(0xFF16A34A) : const Color(0xFFD97706),
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          children: [
            const Align(
              alignment: Alignment.centerRight,
              child: Text(
                'البيانات المستخرجة',
                style: TextStyle(color: Color(0xFF94A3B8), fontSize: 13, fontWeight: FontWeight.bold),
              ),
            ),
            const SizedBox(height: 12),
            Container(
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.grey.shade100, width: 1.5),
              ),
              child: Column(
                children: [
                  _buildDataRow(
                    'التصنيف', 
                    '${op.isOutgoing ? "صادرة ↑" : "واردة ↓"} - ${op.category ?? "غير محدد"}', 
                    isBold: true
                  ),
                  _buildDivider(),
                  _buildDataRow('المبلغ', format.format(amount), isBold: true),
                  _buildDivider(),
                  _buildDataRow('العملة', (op.currency != null && op.currency!.isNotEmpty) ? op.currency! : 'غير محدد', isBold: true),
                  _buildDivider(),
                  _buildDataRow('الطرف', op.party ?? 'غير محدد'),
                  _buildDivider(),
                  _buildDataRow('الرقم المرسل', _formatDisplayNumber(op.senderNumber)),
                  _buildDivider(),
                  _buildDataRow('الرقم المستقبل', _formatDisplayNumber(op.receiverNumber)),
                  _buildDivider(),
                  _buildDataRow('المصدر', op.source ?? 'واتساب'),
                  _buildDivider(),
                  _buildDataRow(
                    'التاريخ',
                    DateFormat('dd MMMM yyyy').format(op.createdAt),
                  ),
                  _buildDivider(),
                  _buildDataRow(
                    'الوقت',
                    DateFormat('hh:mm a').format(op.createdAt).replaceAll('AM', 'ص').replaceAll('PM', 'م'),
                  ),
                ],
              ),
            ),
            if (op.notes != null && op.notes!.isNotEmpty) ...[
              const SizedBox(height: 24),
              const Align(
                alignment: Alignment.centerRight,
                child: Text(
                  'ملاحظات',
                  style: TextStyle(color: Color(0xFF94A3B8), fontSize: 13, fontWeight: FontWeight.bold),
                ),
              ),
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade100, width: 1.5),
                ),
                child: Text(
                  op.notes!,
                  textAlign: TextAlign.right,
                  style: const TextStyle(color: Color(0xFF1E293B), fontSize: 14, height: 1.5),
                ),
              ),
            ],
            const SizedBox(height: 24),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFFF0F9FF),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFBAE6FD)),
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: const [
                      Icon(Icons.chat_bubble_outline, color: Color(0xFF0284C7), size: 16),
                      SizedBox(width: 8),
                      Text(
                        'الرسالة الأصلية',
                        style: TextStyle(color: Color(0xFF0284C7), fontWeight: FontWeight.bold, fontSize: 13),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Text(
                    '«${op.cleanRawMessage.isNotEmpty ? op.cleanRawMessage : (op.rawMessage ?? "")}»',
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: Color(0xFF0369A1), fontSize: 14, fontWeight: FontWeight.w500),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDivider() {
    return const Divider(height: 1, color: Color(0xFFF1F5F9), indent: 16, endIndent: 16);
  }

  Widget _buildDataRow(String label, String value, {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: const TextStyle(color: Color(0xFF94A3B8), fontSize: 13, fontWeight: FontWeight.w500),
          ),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.left,
              style: TextStyle(
                color: const Color(0xFF1E293B),
                fontSize: 14,
                fontWeight: isBold ? FontWeight.bold : FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }
}