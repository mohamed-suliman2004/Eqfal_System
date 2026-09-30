import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../providers/operations_provider.dart';
import '../models/operation.dart';

class AnalyzeMessageScreen extends ConsumerStatefulWidget {
  const AnalyzeMessageScreen({super.key});

  @override
  ConsumerState<AnalyzeMessageScreen> createState() => _AnalyzeMessageScreenState();
}

class _AnalyzeMessageScreenState extends ConsumerState<AnalyzeMessageScreen> {
  final _messageController = TextEditingController();
  bool _isAnalyzing = false;
  bool _isSaving = false;
  Map<String, dynamic>? _analyzedResult;

  Future<void> _analyzeMessage() async {
    final text = _messageController.text.trim();
    if (text.isEmpty) return;

    setState(() {
      _isAnalyzing = true;
      _analyzedResult = null;
    });

    final apiService = ref.read(apiServiceProvider);
    final result = await apiService.analyzeMessage(text);

    if (mounted) {
      setState(() {
        _isAnalyzing = false;
        if (result != null) {
          _analyzedResult = result;
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('حدث خطأ أثناء التحليل.')),
          );
        }
      });
    }
  }

  Future<void> _saveAsDraftAndEdit() async {
    await _saveOperation(true);
  }

  Future<void> _saveAsDraftAndExit() async {
    await _saveOperation(false);
  }

  Future<void> _saveOperation(bool navigateToEdit) async {
    if (_analyzedResult == null) return;
    
    setState(() {
      _isSaving = true;
    });

    final apiService = ref.read(apiServiceProvider);
    final text = _messageController.text.trim();

    final operation = Operation(
      id: 0,
      userId: 1,
      category: _analyzedResult!['category'],
      amount: _analyzedResult!['amount'] != null ? (_analyzedResult!['amount'] as num).toDouble() : null,
      currency: _analyzedResult!['currency'],
      party: _analyzedResult!['party'],
      senderNumber: _analyzedResult!['senderNumber'],
      receiverNumber: _analyzedResult!['receiverNumber'],
      source: _analyzedResult!['source'],
      notes: _analyzedResult!['notes'],
      status: 'مسودة', // Always save as draft from this screen per design
      rawMessage: text,
      createdAt: DateTime.now(),
      isReviewed: false,
    );

    final newId = await apiService.createOperation(operation);
    
    if (mounted) {
      setState(() {
        _isSaving = false;
      });
      
      if (newId != null) {
        ref.refresh(operationsProvider);
        if (navigateToEdit) {
          context.pushReplacement('/operations/$newId/edit');
        } else {
          context.pop();
        }
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('حدث خطأ أثناء الحفظ.')),
        );
      }
    }
  }

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('تحليل رسالة', style: TextStyle(color: Colors.black87, fontWeight: FontWeight.bold, fontSize: 18)),
        centerTitle: true,
        backgroundColor: Colors.white,
        iconTheme: const IconThemeData(color: Colors.black87),
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          children: [
            Container(
              height: 180,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.grey.shade200, width: 1.5),
              ),
              child: TextField(
                controller: _messageController,
                maxLines: null,
                expands: true,
                textAlign: TextAlign.right,
                textDirection: TextDirection.rtl,
                decoration: const InputDecoration(
                  hintText: 'الصق الرسالة هنا...',
                  hintStyle: TextStyle(color: Color(0xFFCBD5E1), fontSize: 14),
                  border: InputBorder.none,
                  contentPadding: EdgeInsets.all(20),
                ),
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton.icon(
                onPressed: _isAnalyzing ? null : _analyzeMessage,
                icon: _isAnalyzing
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                    : const Icon(Icons.auto_awesome, size: 20),
                label: Text(
                  _isAnalyzing ? 'جاري التحليل...' : 'تحليل الرسالة',
                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF3B82F6),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  elevation: 0,
                ),
              ),
            ),
            
            if (_analyzedResult != null) ...[
              const SizedBox(height: 24),
              _buildExtractedInfoCard(),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildExtractedInfoCard() {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade200, width: 1.5),
      ),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16.0),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF1F5F9),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Row(
                    children: const [
                      Text('ثقة متوسطة', style: TextStyle(fontSize: 12, color: Color(0xFF475569), fontWeight: FontWeight.bold)),
                      SizedBox(width: 4),
                      Icon(Icons.help_outline, size: 14, color: Color(0xFF475569)),
                    ],
                  ),
                ),
                const Text('المعلومات المستخرجة', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF1E293B))),
              ],
            ),
          ),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('التصنيف', _analyzedResult!['category']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('المبلغ', _analyzedResult!['amount']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('العملة', _analyzedResult!['currency']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('الطرف', _analyzedResult!['party']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('الرقم المرسل', _analyzedResult!['senderNumber']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('الرقم المستقبل', _analyzedResult!['receiverNumber']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('الملاحظات', _analyzedResult!['notes']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          _buildInfoRow('طريقة الإدخال', _analyzedResult!['source']?.toString() ?? '---'),
          const Divider(height: 1, thickness: 1, color: Color(0xFFF1F5F9)),
          
          Padding(
            padding: const EdgeInsets.all(16.0),
            child: Column(
              children: [
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    onPressed: _isSaving ? null : _saveAsDraftAndEdit,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF3B82F6),
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      elevation: 0,
                    ),
                    child: _isSaving 
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                        : Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: const [
                              Icon(Icons.chevron_left, size: 18),
                              SizedBox(width: 8),
                              Text('استكمال البيانات', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                            ],
                          ),
                  ),
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: OutlinedButton(
                    onPressed: _isSaving ? null : _saveAsDraftAndExit,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: const Color(0xFF64748B),
                      side: const BorderSide(color: Color(0xFFE2E8F0)),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    child: const Text('إرسال إلى يحتاج إكمال', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 14.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF0F172A)),
          ),
          Text(
            label,
            style: const TextStyle(fontSize: 14, color: Color(0xFF94A3B8)),
          ),
        ],
      ),
    );
  }
}
