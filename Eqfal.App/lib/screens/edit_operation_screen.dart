import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../models/operation.dart';
import '../providers/operations_provider.dart';
// import '../services/api_service.dart';
import 'package:go_router/go_router.dart';

class EditOperationScreen extends ConsumerStatefulWidget {
  final String id;

  const EditOperationScreen({super.key, required this.id});

  @override
  ConsumerState<EditOperationScreen> createState() => _EditOperationScreenState();
}

class _EditOperationScreenState extends ConsumerState<EditOperationScreen> {
  final _formKey = GlobalKey<FormState>();
  bool isLoading = true;
  Operation? operation;

  // Form Controllers
  final _amountController = TextEditingController();
  final _currencyController = TextEditingController();
  final _partyController = TextEditingController();
  final _senderController = TextEditingController();
  final _receiverController = TextEditingController();
  final _sourceController = TextEditingController();
  final _notesController = TextEditingController();
  
  String? _selectedCategory;

  // Only 'تسليم' and 'استلام' as requested by the user
  static const List<String> supportedCategories = [
    'تسليم',
    'استلام',
  ];

  static const List<Map<String, String>> currencies = [
    // الخليجية والعربية
    {'code': 'LYD', 'name': 'دينار ليبي (LYD)'},
    {'code': 'SAR', 'name': 'ريال سعودي (SAR)'},
    {'code': 'AED', 'name': 'درهم إماراتي (AED)'},
    {'code': 'QAR', 'name': 'ريال قطري (QAR)'},
    {'code': 'KWD', 'name': 'دينار كويتي (KWD)'},
    {'code': 'BHD', 'name': 'دينار بحريني (BHD)'},
    {'code': 'OMR', 'name': 'ريال عماني (OMR)'},
    {'code': 'JOD', 'name': 'دينار أردني (JOD)'},
    {'code': 'IQD', 'name': 'دينار عراقي (IQD)'},
    {'code': 'TND', 'name': 'دينار تونسي (TND)'},
    {'code': 'MAD', 'name': 'درهم مغربي (MAD)'},
    {'code': 'DZD', 'name': 'دينار جزائري (DZD)'},
    {'code': 'EGP', 'name': 'جنيه مصري (EGP)'},
    {'code': 'SDG', 'name': 'جنيه سوداني (SDG)'},
    {'code': 'LBP', 'name': 'ليرة لبنانية (LBP)'},
    {'code': 'SYP', 'name': 'ليرة سورية (SYP)'},
    {'code': 'TRY', 'name': 'ليرة تركية (TRY)'},
    {'code': 'YER', 'name': 'ريال يمني (YER)'},
    // العالمية
    {'code': 'USD', 'name': 'دولار أمريكي (USD - \$)'},
    {'code': 'EUR', 'name': 'يورو (EUR - €)'},
    {'code': 'CNY', 'name': 'يوان صيني (CNY - ¥)'},
    {'code': 'JPY', 'name': 'ين ياباني (JPY - ¥)'},
    {'code': 'GBP', 'name': 'جنيه إسترليني (GBP - £)'},
    {'code': 'CHF', 'name': 'فرنك سويسري (CHF)'},
    {'code': 'CAD', 'name': 'دولار كندي (CAD - C\$)'},
    {'code': 'AUD', 'name': 'دولار أسترالي (AUD - A\$)'},
    {'code': 'RUB', 'name': 'روبل روسي (RUB - ₽)'},
    {'code': 'INR', 'name': 'روبية هندية (INR - ₹)'},
    {'code': 'USDT', 'name': 'تيذر رقمي (USDT - ₮)'},
    {'code': 'BYN', 'name': 'روبل بيلاروسي (BYN - Br)'},
  ];

  @override
  void initState() {
    super.initState();
    _fetchOperation();
  }

  Future<void> _fetchOperation() async {
    final int opId = int.tryParse(widget.id) ?? 0;
    final operations = ref.read(operationsProvider).value ?? [];
    try {
      final op = operations.firstWhere((o) => o.id == opId);
      setState(() {
        operation = op;
        String cat = (op.category ?? '').trim();
        if (cat == 'صرف' || cat == 'دفع' || cat == 'تحويل' || cat == 'سحب') {
          cat = 'تسليم';
        } else if (cat == 'قبض' || cat == 'إيداع' || cat == 'ايداع' || cat == 'شحن') {
          cat = 'استلام';
        }
        _selectedCategory = (cat.isNotEmpty && supportedCategories.contains(cat)) 
            ? cat 
            : 'تسليم';

        _amountController.text = (op.amount != null && op.amount! > 0) ? op.amount.toString() : '';
        _currencyController.text = (op.currency != null && op.currency!.isNotEmpty) ? op.currency! : 'LYD';
        _partyController.text = op.party ?? '';
        _senderController.text = op.senderNumber ?? '';
        _receiverController.text = op.receiverNumber ?? '';
        _sourceController.text = op.source ?? 'واتساب';
        _notesController.text = op.notes ?? '';
        isLoading = false;
      });
    } catch (e) {
      setState(() {
        isLoading = false;
      });
    }
  }

  @override
  void dispose() {
    _amountController.dispose();
    _currencyController.dispose();
    _partyController.dispose();
    _senderController.dispose();
    _receiverController.dispose();
    _sourceController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _saveAndComplete() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_currencyController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى تحديد أو كتابة رمز العملة')),
      );
      return;
    }

    if (operation == null) return;

    setState(() => isLoading = true);

    try {
      final updatedOp = operation!.copyWith(
        category: _selectedCategory ?? 'تسليم',
        amount: double.tryParse(_amountController.text) ?? 0,
        currency: _currencyController.text.trim(),
        party: _partyController.text.trim(),
        senderNumber: _senderController.text.trim(),
        receiverNumber: _receiverController.text.trim(),
        source: _sourceController.text.trim(),
        notes: _notesController.text.trim(),
        status: 'مكتمل',
        isReviewed: false, // Remains in unreviewed list of that category until checked
      );

      final apiService = ref.read(apiServiceProvider);
      final success = await apiService.updateOperation(updatedOp);

      if (!mounted) return;

      if (success) {
        ref.invalidate(operationsProvider);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تم حفظ العملية وتصنيفها بنجاح'),
            backgroundColor: Color(0xFF22C55E),
          ),
        );
        context.pop();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('فشل في حفظ التعديلات')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('حدث خطأ: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => isLoading = false);
    }
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
        appBar: AppBar(title: const Text('خطأ')),
        body: const Center(child: Text('العملية غير موجودة')),
      );
    }

    final formattedDate = DateFormat('hh:mm a').format(operation!.createdAt)
        .replaceAll('AM', 'ص').replaceAll('PM', 'م');

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text(
          'تعديل العملية',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Colors.black87),
        ),
        centerTitle: true,
        backgroundColor: Colors.transparent,
        elevation: 0,
        leading: Padding(
          padding: const EdgeInsets.only(left: 16.0),
          child: Container(
            margin: const EdgeInsets.symmetric(vertical: 8),
            decoration: BoxDecoration(
              color: Colors.orange.shade50,
              borderRadius: BorderRadius.circular(16),
            ),
            alignment: Alignment.center,
            child: const Text(
              'مراجعة مطلوبة',
              style: TextStyle(
                color: Color(0xFFEA580C),
                fontSize: 11,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ),
        leadingWidth: 110,
        actions: [
          IconButton(
            icon: const Icon(Icons.arrow_forward, color: Colors.black87),
            onPressed: () => context.pop(),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Original Message Box
              Container(
                padding: const EdgeInsets.all(16.0),
                decoration: BoxDecoration(
                  color: const Color(0xFFF0F7FF),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: const Color(0xFFE0EEFE)),
                ),
                child: Column(
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          formattedDate,
                          style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
                        ),
                        Row(
                          children: const [
                            Text(
                              'الرسالة الأصلية',
                              style: TextStyle(color: Color(0xFF2563EB), fontSize: 12, fontWeight: FontWeight.bold),
                            ),
                            SizedBox(width: 4),
                            Icon(Icons.chat_bubble_outline, color: Color(0xFF2563EB), size: 14),
                          ],
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      '«${operation!.cleanRawMessage.isNotEmpty ? operation!.cleanRawMessage : (operation!.rawMessage ?? "")}»',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: Color(0xFF1E293B),
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
              
              const SizedBox(height: 24),
              const Center(
                child: Text(
                  'البيانات المستخرجة',
                  style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12, fontWeight: FontWeight.bold),
                ),
              ),
              const SizedBox(height: 8),
              
              // Form Fields
              Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade100, width: 1.5),
                ),
                child: Column(
                  children: [
                    _buildDropdownRow('التصنيف', 'اختر التصنيف'),
                    _buildDivider(),
                    _buildTextFieldRow('المبلغ', _amountController, isNumber: true, alignRight: false),
                    _buildDivider(),
                    _buildCurrencyAutocompleteRow('العملة', _currencyController),
                    _buildDivider(),
                    _buildTextFieldRow('الطرف', _partyController, hint: 'اسم الشخص أو الشركة'),
                    _buildDivider(),
                    _buildTextFieldRow('الرقم المرسل', _senderController, isNumber: true, alignRight: false),
                    _buildDivider(),
                    _buildTextFieldRow('الرقم المستقبل', _receiverController, isNumber: true, alignRight: false),
                    _buildDivider(),
                    _buildTextFieldRow('المصدر', _sourceController),
                  ],
                ),
              ),
              
              const SizedBox(height: 24),
              const Text(
                'ملاحظات',
                textAlign: TextAlign.right,
                style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              
              Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade100, width: 1.5),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                child: TextFormField(
                  controller: _notesController,
                  maxLines: 2,
                  textAlign: TextAlign.right,
                  decoration: const InputDecoration(
                    border: InputBorder.none,
                    hintText: 'أضف ملاحظات...',
                  ),
                ),
              ),
              
              const SizedBox(height: 32),

              // Delete button
              Center(
                child: TextButton.icon(
                  onPressed: () async {
                    final confirm = await showDialog<bool>(
                      context: context,
                      builder: (ctx) => AlertDialog(
                        title: const Text('حذف العملية', textAlign: TextAlign.right),
                        content: const Text('هل أنت متأكد من حذف هذه العملية؟ لا يمكن التراجع عن هذا الإجراء.', textAlign: TextAlign.right),
                        actions: [
                          TextButton(
                            onPressed: () => Navigator.pop(ctx, false),
                            child: const Text('إلغاء'),
                          ),
                          TextButton(
                            onPressed: () => Navigator.pop(ctx, true),
                            style: TextButton.styleFrom(foregroundColor: Colors.red),
                            child: const Text('حذف'),
                          ),
                        ],
                      ),
                    );

                    if (confirm == true && mounted) {
                      setState(() => isLoading = true);
                      final apiService = ref.read(apiServiceProvider);
                      final success = await apiService.deleteOperation(operation!.id);
                      if (!mounted) return;
                      
                      if (success) {
                        ref.invalidate(operationsProvider);
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(content: Text('تم حذف العملية')),
                        );
                        context.pop();
                      } else {
                        setState(() => isLoading = false);
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(content: Text('فشل في حذف العملية')),
                        );
                      }
                    }
                  },
                  icon: const Icon(Icons.delete_outline, color: Color(0xFFEF4444), size: 18),
                  label: const Text(
                    'حذف العملية (مسودة)',
                    style: TextStyle(color: Color(0xFFEF4444), fontSize: 13, fontWeight: FontWeight.bold),
                  ),
                ),
              ),

              const SizedBox(height: 16),
              
              ElevatedButton(
                onPressed: _saveAndComplete,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF3B82F6),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  elevation: 0,
                ),
                child: const Text('حفظ واكتمال', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
              ),
              const SizedBox(height: 40),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildDivider() {
    return const Divider(height: 1, color: Color(0xFFF1F5F9), indent: 16, endIndent: 16);
  }

  Widget _buildTextFieldRow(String label, TextEditingController controller, {bool isNumber = false, bool alignRight = true, String? hint}) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          Expanded(
            child: TextFormField(
              controller: controller,
              keyboardType: isNumber ? const TextInputType.numberWithOptions(decimal: true) : TextInputType.text,
              textAlign: alignRight ? TextAlign.right : TextAlign.left,
              decoration: InputDecoration(
                border: InputBorder.none,
                hintText: hint ?? label,
                hintStyle: TextStyle(color: Colors.grey.shade400, fontSize: 14),
                contentPadding: const EdgeInsets.symmetric(vertical: 12),
              ),
              validator: (val) {
                if (label == 'المبلغ' && (val == null || val.trim().isEmpty)) {
                  return 'يرجى إدخال المبلغ';
                }
                return null;
              },
            ),
          ),
          const SizedBox(width: 16),
          SizedBox(
            width: 110,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                if (label == 'المبلغ' || label == 'الطرف')
                  const Text(' *', style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold)),
                Text(
                  label,
                  style: const TextStyle(color: Color(0xFF64748B), fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDropdownRow(String label, String hint) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          Expanded(
            child: DropdownButtonFormField<String>(
              value: (_selectedCategory != null && supportedCategories.contains(_selectedCategory)) 
                  ? _selectedCategory 
                  : supportedCategories.first,
              items: supportedCategories.map((cat) => DropdownMenuItem(
                value: cat, 
                child: Text(cat, textAlign: TextAlign.right),
              )).toList(),
              onChanged: (val) => setState(() => _selectedCategory = val),
              decoration: const InputDecoration(border: InputBorder.none),
              hint: Text(hint, style: TextStyle(color: Colors.grey.shade400, fontSize: 14)),
              icon: const Icon(Icons.arrow_drop_down, color: Color(0xFF64748B)),
              validator: (val) => (val == null || val.isEmpty) ? 'يرجى اختيار التصنيف' : null,
            ),
          ),
          const SizedBox(width: 16),
          SizedBox(
            width: 110,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                const Text(' *', style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold)),
                Text(
                  label,
                  style: const TextStyle(color: Color(0xFF64748B), fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCurrencyAutocompleteRow(String label, TextEditingController controller) {
    final currentVal = controller.text.trim().toUpperCase();
    final allCurrencies = List<Map<String, String>>.from(currencies);
    if (currentVal.isNotEmpty && !allCurrencies.any((c) => c['code'] == currentVal)) {
      allCurrencies.insert(0, {'code': currentVal, 'name': '$currentVal (مخصص)'});
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          Expanded(
            child: Autocomplete<Map<String, String>>(
              initialValue: TextEditingValue(text: controller.text),
              displayStringForOption: (option) => option['code']!,
              optionsBuilder: (TextEditingValue textEditingValue) {
                if (textEditingValue.text.isEmpty) {
                  return allCurrencies;
                }
                return allCurrencies.where((c) =>
                    c['name']!.toLowerCase().contains(textEditingValue.text.toLowerCase()) ||
                    c['code']!.toLowerCase().contains(textEditingValue.text.toLowerCase()));
              },
              onSelected: (option) {
                controller.text = option['code']!;
              },
              fieldViewBuilder: (context, fieldTextEditingController, fieldFocusNode, onFieldSubmitted) {
                fieldTextEditingController.addListener(() {
                  controller.text = fieldTextEditingController.text;
                });
                
                if (fieldTextEditingController.text.isEmpty && controller.text.isNotEmpty) {
                  fieldTextEditingController.text = controller.text;
                }

                return TextFormField(
                  controller: fieldTextEditingController,
                  focusNode: fieldFocusNode,
                  textAlign: TextAlign.left,
                  textDirection: TextDirection.ltr,
                  decoration: InputDecoration(
                    border: InputBorder.none,
                    hintText: 'LYD, USD, EUR...',
                    hintTextDirection: TextDirection.ltr,
                    hintStyle: TextStyle(color: Colors.grey.shade400, fontSize: 14),
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  validator: (val) => (val == null || val.trim().isEmpty) ? 'يرجى إدخال العملة' : null,
                );
              },
            ),
          ),
          const SizedBox(width: 16),
          SizedBox(
            width: 110,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                const Text(' *', style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold)),
                Text(
                  label,
                  style: const TextStyle(color: Color(0xFF64748B), fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
