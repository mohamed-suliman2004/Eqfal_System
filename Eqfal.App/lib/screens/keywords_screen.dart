import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/keyword.dart';
import '../providers/operations_provider.dart';

class KeywordsScreen extends ConsumerStatefulWidget {
  final String category;
  const KeywordsScreen({super.key, required this.category});

  @override
  ConsumerState<KeywordsScreen> createState() => _KeywordsScreenState();
}

class _KeywordsScreenState extends ConsumerState<KeywordsScreen> {
  final _keywordController = TextEditingController();
  List<Keyword> keywords = [];
  bool isLoading = true;

  String get _normalizedCategory {
    String cat = widget.category;
    try {
      cat = Uri.decodeComponent(cat);
    } catch (_) {}
    cat = cat.trim();

    if (cat == 'تسليم' || cat.toLowerCase() == 'delivery' || cat.toLowerCase() == 'expense') {
      return 'تسليم';
    }
    if (cat == 'استلام' || cat.toLowerCase() == 'receipt' || cat.toLowerCase() == 'income') {
      return 'استلام';
    }
    return cat;
  }

  @override
  void initState() {
    super.initState();
    _loadKeywords();
  }

  Future<void> _loadKeywords() async {
    final apiService = ref.read(apiServiceProvider);
    final data = await apiService.getKeywords(_normalizedCategory);
    
    if (mounted) {
      setState(() {
        keywords = data.map((json) => Keyword.fromJson(json)).toList();
        isLoading = false;
      });
    }
  }

  Future<void> _addKeyword() async {
    final word = _keywordController.text.trim();
    if (word.isEmpty) return;

    final apiService = ref.read(apiServiceProvider);
    final success = await apiService.addKeyword(word, _normalizedCategory);

    if (success) {
      _keywordController.clear();
      _loadKeywords();
      ref.invalidate(keywordsProvider);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تمت إضافة "$word" إلى قاموس $_normalizedCategory')),
      );
    } else {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('فشل إضافة الكلمة، قد تكون مكررة')),
        );
      }
    }
  }

  Future<void> _deleteKeyword(int id) async {
    setState(() {
      keywords.removeWhere((k) => k.id == id);
    });

    final apiService = ref.read(apiServiceProvider);
    final success = await apiService.deleteKeyword(id);

    if (success) {
      ref.invalidate(keywordsProvider);
    } else {
      _loadKeywords();
    }
  }

  @override
  void dispose() {
    _keywordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: Text('قاموس $_normalizedCategory', style: const TextStyle(color: Colors.black87, fontWeight: FontWeight.bold, fontSize: 18)),
        centerTitle: true,
        backgroundColor: Colors.white,
        iconTheme: const IconThemeData(color: Colors.black87),
        elevation: 0,
      ),
      body: isLoading
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Center(
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          _normalizedCategory,
                          style: TextStyle(
                            color: _normalizedCategory == 'استلام' ? const Color(0xFF10B981) : const Color(0xFF3B82F6),
                            fontWeight: FontWeight.bold,
                            fontSize: 16,
                          ),
                        ),
                        const SizedBox(width: 8),
                        const Text(
                          'الكلمات الدالة على عمليات:',
                          style: TextStyle(color: Color(0xFF94A3B8), fontSize: 14),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 24),

                  // Input box
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
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
                        Expanded(
                          child: TextField(
                            controller: _keywordController,
                            textAlign: TextAlign.right,
                            decoration: InputDecoration(
                              hintText: 'اكتب الكلمة هنا...',
                              hintStyle: TextStyle(color: Colors.grey.shade400, fontSize: 14),
                              border: InputBorder.none,
                            ),
                            onSubmitted: (_) => _addKeyword(),
                          ),
                        ),
                        const SizedBox(width: 8),
                        ElevatedButton.icon(
                          onPressed: _addKeyword,
                          icon: const Icon(Icons.add, size: 18),
                          label: const Text('إضافة', style: TextStyle(fontWeight: FontWeight.bold)),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF2563EB),
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                            elevation: 0,
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 24),
                  const Text('الكلمات الحالية:', style: TextStyle(color: Color(0xFF64748B), fontWeight: FontWeight.bold, fontSize: 14)),
                  const SizedBox(height: 12),

                  // Keywords Chips List
                  Expanded(
                    child: keywords.isEmpty
                        ? const Center(child: Text('لا توجد كلمات مضافة حالياً', style: TextStyle(color: Colors.grey)))
                        : SingleChildScrollView(
                            child: Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: keywords.map((k) {
                                return Chip(
                                  label: Text(k.word, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                                  deleteIcon: const Icon(Icons.close, size: 16, color: Colors.red),
                                  onDeleted: () => _deleteKeyword(k.id),
                                  backgroundColor: Colors.white,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(12),
                                    side: BorderSide(color: Colors.grey.shade200),
                                  ),
                                );
                              }).toList(),
                            ),
                          ),
                  ),
                ],
              ),
            ),
    );
  }
}