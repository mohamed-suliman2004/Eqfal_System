import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/keyword.dart';
import '../providers/operations_provider.dart';

class CurrenciesScreen extends ConsumerStatefulWidget {
  const CurrenciesScreen({super.key});

  @override
  ConsumerState<CurrenciesScreen> createState() => _CurrenciesScreenState();
}

class _CurrenciesScreenState extends ConsumerState<CurrenciesScreen> {
  final _searchController = TextEditingController();
  List<Keyword> currencies = [];
  bool isLoading = true;
  String _searchQuery = '';

  // 30 عملة عربية وخليجية وعالمية معتمدة
  static const Map<String, String> worldCurrencies = {
    // الخليجية والعربية
    'LYD': 'دينار ليبي',
    'SAR': 'ريال سعودي',
    'AED': 'درهم إماراتي',
    'QAR': 'ريال قطري',
    'KWD': 'دينار كويتي',
    'BHD': 'دينار بحريني',
    'OMR': 'ريال عماني',
    'JOD': 'دينار أردني',
    'IQD': 'دينار عراقي',
    'TND': 'دينار تونسي',
    'MAD': 'درهم مغربي',
    'DZD': 'دينار جزائري',
    'EGP': 'جنيه مصري',
    'SDG': 'جنيه سوداني',
    'LBP': 'ليرة لبنانية',
    'SYP': 'ليرة سورية',
    'TRY': 'ليرة تركية',
    'YER': 'ريال يمني',
    // العالمية الرئيسية
    'USD': 'دولار أمريكي',
    'EUR': 'يورو',
    'CNY': 'يوان صيني',
    'JPY': 'ين ياباني',
    'GBP': 'جنيه إسترليني',
    'CHF': 'فرنك سويسري',
    'CAD': 'دولار كندي',
    'AUD': 'دولار أسترالي',
    'RUB': 'روبل روسي',
    'INR': 'روبية هندية',
    'USDT': 'تيذر رقمي',
    'BYN': 'روبل بيلاروسي',
  };

  // الرموز الافتراضية لكل عملة
  static const Map<String, List<String>> defaultSymbols = {
    'LYD': ['دينار ليبي', 'دينار', 'د.ل', 'دل', 'lyd'],
    'SAR': ['ريال سعودي', 'ريال', 'س.ر', 'ر.س', 'sar'],
    'AED': ['درهم إماراتي', 'درهم', 'د.إ', 'aed'],
    'QAR': ['ريال قطري', 'ر.ق', 'qar'],
    'KWD': ['دينار كويتي', 'د.ك', 'kwd'],
    'BHD': ['دينار بحريني', 'د.ب', 'bhd'],
    'OMR': ['ريال عماني', 'ر.ع', 'omr'],
    'JOD': ['دينار أردني', 'د.أ', 'jod'],
    'IQD': ['دينار عراقي', 'د.ع', 'iqd'],
    'TND': ['دينار تونسي', 'د.ت', 'tnd', 'تونس'],
    'MAD': ['درهم مغربي', 'د.م', 'mad', 'مغرب'],
    'DZD': ['دينار جزائري', 'د.ج', 'dzd'],
    'EGP': ['جنيه مصري', 'جنيه', 'ج.م', 'egp'],
    'SDG': ['جنيه سوداني', 'ج.س', 'sdg'],
    'LBP': ['ليرة لبنانية', 'ل.ل', 'lbp'],
    'SYP': ['ليرة سورية', 'ل.س', 'syp'],
    'TRY': ['ليرة تركية', 'ليرة', 'ليره', 'تركي', 'try', 'tl'],
    'YER': ['ريال يمني', 'ر.ي', 'yer'],
    'USD': ['دولار أمريكي', 'دولار', r'$', 'usd', 'dollar'],
    'EUR': ['يورو', '€', 'eur'],
    'CNY': ['يوان صيني', 'ين صيني', 'يوان', 'ين', '¥', 'cny', 'rmb'],
    'JPY': ['ين ياباني', 'jpy', 'yen'],
    'GBP': ['جنيه إسترليني', 'باوند', 'استرليني', '£', 'gbp'],
    'CHF': ['فرنك سويسري', 'فرنك', 'chf'],
    'CAD': ['دولار كندي', 'cad', r'c$'],
    'AUD': ['دولار أسترالي', 'aud', r'a$'],
    'RUB': ['روبل روسي', 'روبل', '₽', 'rub'],
    'INR': ['روبية هندية', 'روبية', '₹', 'inr'],
    'USDT': ['تيذر رقمي', 'تيذر', 'usdt'],
    'BYN': ['روبل بيلاروسي', 'ر.ب', 'byn', 'byr'],
  };

  @override
  void initState() {
    super.initState();
    _loadCurrencies();
  }

  Future<void> _loadCurrencies({bool autoSyncIfEmpty = true}) async {
    final apiService = ref.read(apiServiceProvider);
    final data = await apiService.getKeywords('');

    if (mounted) {
      final loaded = data
          .map((json) => Keyword.fromJson(json))
          .where((k) => k.type != 'استلام' && k.type != 'تسليم')
          .toList();

      if (loaded.isEmpty && autoSyncIfEmpty) {
        // إذا كان المستخدم لا يملك أي عملات مسجلة، نقوم بمزامنة العملات الافتراضية تلقائياً
        await apiService.syncDefaultCurrencies();
        _loadCurrencies(autoSyncIfEmpty: false);
        return;
      }

      setState(() {
        currencies = loaded;
        isLoading = false;
      });
    }
  }

  // مزامنة كافة العملات الافتراضية الـ 30
  Future<void> _syncDefaults() async {
    setState(() => isLoading = true);
    final apiService = ref.read(apiServiceProvider);
    final ok = await apiService.syncDefaultCurrencies();
    await _loadCurrencies(autoSyncIfEmpty: false);
    ref.invalidate(keywordsProvider);

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(ok ? 'تمت مزامنة العملات والرموز الافتراضية بنجاح' : 'تعذرت المزامنة، يرجى المحاولة لاحقاً'),
          backgroundColor: ok ? const Color(0xFF059669) : const Color(0xFFDC2626),
        ),
      );
    }
  }

  // تفعيل عملة قياسية بنقرة واحدة
  Future<void> _activateCurrency(String code) async {
    final symbols = defaultSymbols[code] ?? [code];
    final apiService = ref.read(apiServiceProvider);
    bool anyAdded = false;

    for (final sym in symbols) {
      final ok = await apiService.addKeyword(sym, code);
      if (ok) anyAdded = true;
    }

    if (anyAdded) {
      _loadCurrencies(autoSyncIfEmpty: false);
      ref.invalidate(keywordsProvider);
      if (mounted) {
        final name = worldCurrencies[code] ?? code;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('تم تفعيل عملة $name ($code) ورموزها بنجاح'),
            backgroundColor: const Color(0xFF059669),
          ),
        );
      }
    }
  }

  // إضافة عملة مخصصة (غير موجودة في الـ 30)
  Future<void> _showAddCustomCurrencyDialog() async {
    final nameController = TextEditingController();
    final codeController = TextEditingController();

    await showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.add_circle, color: Color(0xFF2563EB), size: 22),
            SizedBox(width: 8),
            Text('إضافة عملة مخصصة', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'أدخل كود العملة والرموز المرادفة لها:',
              style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: codeController,
              textAlign: TextAlign.center,
              textCapitalization: TextCapitalization.characters,
              style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF2563EB), letterSpacing: 1.5),
              decoration: InputDecoration(
                labelText: 'كود العملة (مثال: BTC, OMR, KWD)',
                filled: true,
                fillColor: const Color(0xFFF8FAFC),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: nameController,
              textAlign: TextAlign.right,
              decoration: InputDecoration(
                labelText: 'الرموز أو المرادفات (مفصولة بفاصلة)',
                hintText: 'مثال: بيتكوين, btc',
                filled: true,
                fillColor: const Color(0xFFF8FAFC),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () async {
              final code = codeController.text.trim().toUpperCase();
              final rawName = nameController.text.trim();
              if (code.isEmpty || rawName.isEmpty) return;

              final symbols = rawName.split(RegExp(r'[,،]')).map((s) => s.trim()).where((s) => s.isNotEmpty).toList();
              if (symbols.isEmpty) symbols.add(rawName);

              Navigator.pop(ctx);
              final apiService = ref.read(apiServiceProvider);
              bool added = false;
              for (final sym in symbols) {
                final ok = await apiService.addKeyword(sym, code);
                if (ok) added = true;
              }

              if (added) {
                _loadCurrencies(autoSyncIfEmpty: false);
                ref.invalidate(keywordsProvider);
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('تمت إضافة عملة $code بنجاح'),
                      backgroundColor: const Color(0xFF059669),
                    ),
                  );
                }
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF2563EB),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: const Text('إضافة', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }

  // إضافة رمز لعملة موجودة
  Future<void> _showAddSymbolDialog(String currencyCode) async {
    final controller = TextEditingController();
    final arabicName = worldCurrencies[currencyCode] ?? currencyCode;

    await showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Row(
          children: [
            const Icon(Icons.add_circle_outline, color: Color(0xFF2563EB)),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'إضافة رمز لـ $arabicName ($currencyCode)',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'اكتب الرمز أو الكلمة المرادفة (يمكن كتابة عدة رموز مفصولة بفاصلة):',
              style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              textAlign: TextAlign.right,
              autofocus: true,
              decoration: InputDecoration(
                hintText: 'مثال: دينار، د.ل، دل',
                filled: true,
                fillColor: const Color(0xFFF8FAFC),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: Colors.grey.shade300)),
                focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: const BorderSide(color: Color(0xFF2563EB), width: 1.5)),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () async {
              final val = controller.text.trim();
              if (val.isEmpty) return;

              final symbols = val.split(RegExp(r'[,،]')).map((s) => s.trim()).where((s) => s.isNotEmpty).toList();
              if (symbols.isEmpty) return;

              Navigator.pop(ctx);
              final apiService = ref.read(apiServiceProvider);
              bool added = false;
              for (final sym in symbols) {
                final ok = await apiService.addKeyword(sym, currencyCode);
                if (ok) added = true;
              }

              if (added) {
                _loadCurrencies(autoSyncIfEmpty: false);
                ref.invalidate(keywordsProvider);
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('تمت إضافة الرموز بنجاح لـ $currencyCode'),
                      backgroundColor: const Color(0xFF059669),
                    ),
                  );
                }
              } else {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('الرمز موجود مسبقاً أو تعذر الحفظ'),
                      backgroundColor: Color(0xFFDC2626),
                    ),
                  );
                }
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF2563EB),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: const Text('إضافة', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }

  // تعديل رمز محدد
  Future<void> _showEditSymbolDialog(Keyword keyword) async {
    final controller = TextEditingController(text: keyword.word);

    await showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.edit, color: Color(0xFF2563EB), size: 20),
            SizedBox(width: 8),
            Text('تعديل الرمز / الكلمة', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('العملة التابع لها: ${keyword.type}', style: const TextStyle(fontSize: 12, color: Color(0xFF64748B), fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              textAlign: TextAlign.right,
              autofocus: true,
              decoration: InputDecoration(
                labelText: 'نص الرمز أو الكلمة',
                filled: true,
                fillColor: const Color(0xFFF8FAFC),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: const BorderSide(color: Color(0xFF2563EB), width: 1.5)),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () async {
              final newWord = controller.text.trim();
              if (newWord.isEmpty || newWord == keyword.word) {
                Navigator.pop(ctx);
                return;
              }

              Navigator.pop(ctx);
              final apiService = ref.read(apiServiceProvider);
              final success = await apiService.updateKeyword(keyword.id, newWord, keyword.type);

              if (success) {
                _loadCurrencies(autoSyncIfEmpty: false);
                ref.invalidate(keywordsProvider);
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('تم تعديل الرمز إلى "$newWord"'),
                      backgroundColor: const Color(0xFF059669),
                    ),
                  );
                }
              } else {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('فشل تعديل الرمز'), backgroundColor: Color(0xFFDC2626)),
                  );
                }
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF2563EB),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: const Text('حفظ التعديل', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }

  // تعديل اسم / كود العملة وتحديث جميع الرموز التابعة لها
  Future<void> _showEditCurrencyCodeDialog(String currentCode) async {
    final controller = TextEditingController(text: currentCode);
    final currentArabic = worldCurrencies[currentCode] ?? currentCode;

    await showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Row(
          children: [
            const Icon(Icons.currency_exchange, color: Color(0xFF2563EB), size: 22),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'تعديل كود عملة $currentArabic',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'سيتم تعديل وتوحيد كود العملة لكافة الرموز المرتبطة بها تلقائياً:',
              style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: controller,
              textAlign: TextAlign.center,
              autofocus: true,
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16, letterSpacing: 1.5, color: Color(0xFF2563EB)),
              decoration: InputDecoration(
                labelText: 'كود العملة الجديد (مثال: LYD, USD, EUR)',
                filled: true,
                fillColor: const Color(0xFFF8FAFC),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: const BorderSide(color: Color(0xFF2563EB), width: 1.5)),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () async {
              final newCode = controller.text.trim().toUpperCase();
              if (newCode.isEmpty || newCode == currentCode) {
                Navigator.pop(ctx);
                return;
              }

              Navigator.pop(ctx);
              final apiService = ref.read(apiServiceProvider);
              final success = await apiService.renameCurrencyType(currentCode, newCode);

              if (success) {
                _loadCurrencies(autoSyncIfEmpty: false);
                ref.invalidate(keywordsProvider);
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('تم تحديث كود العملة من $currentCode إلى $newCode بنجاح'),
                      backgroundColor: const Color(0xFF059669),
                    ),
                  );
                }
              } else {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('فشل تعديل كود العملة'), backgroundColor: Color(0xFFDC2626)),
                  );
                }
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF2563EB),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: const Text('حفظ الكود', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }

  // حذف رمز فردي
  Future<void> _deleteSymbol(int id, String word) async {
    setState(() {
      currencies.removeWhere((c) => c.id == id);
    });

    final apiService = ref.read(apiServiceProvider);
    final success = await apiService.deleteKeyword(id);

    if (success) {
      ref.invalidate(keywordsProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('تم حذف الرمز "$word"'),
            duration: const Duration(seconds: 2),
          ),
        );
      }
    } else {
      _loadCurrencies(autoSyncIfEmpty: false);
    }
  }

  // حذف العملة بالكامل مع كافة رموزها
  Future<void> _confirmDeleteEntireCurrency(String currencyCode, int symbolsCount) async {
    final arabicName = worldCurrencies[currencyCode] ?? currencyCode;

    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Row(
          children: [
            const Icon(Icons.warning_amber_rounded, color: Color(0xFFDC2626), size: 24),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'حذف عملة $arabicName ($currencyCode)',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFFDC2626)),
              ),
            ),
          ],
        ),
        content: Text(
          'هل أنت متأكد من حذف هذه العملة بالكامل مع جميع الرموز التابعة لها ($symbolsCount رموز)؟\nلن يتم التعرف على هذه العملة في الرسائل بعد حذفها.',
          style: const TextStyle(fontSize: 13, height: 1.5, color: Color(0xFF334155)),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFFDC2626),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: const Text('نعم، حذف العملة بالكامل', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );

    if (confirm == true) {
      final apiService = ref.read(apiServiceProvider);
      final success = await apiService.deleteCurrencyType(currencyCode);

      if (success) {
        _loadCurrencies(autoSyncIfEmpty: false);
        ref.invalidate(keywordsProvider);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('تم حذف عملة $currencyCode وكافة رموزها بنجاح'),
              backgroundColor: const Color(0xFF059669),
            ),
          );
        }
      } else {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('فشل حذف العملة'), backgroundColor: Color(0xFFDC2626)),
          );
        }
      }
    }
  }

  String _getCurrencyAvatar(String code) {
    switch (code.toUpperCase()) {
      case 'USD':
        return r'$';
      case 'EUR':
        return '€';
      case 'GBP':
        return '£';
      case 'CNY':
      case 'JPY':
        return '¥';
      case 'RUB':
        return '₽';
      case 'INR':
        return '₹';
      case 'TRY':
        return '₺';
      case 'BYN':
        return 'Br';
      case 'USDT':
        return '₮';
      default:
        return code.isNotEmpty ? code[0] : '?';
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // تجميع العملات النشطة لدى المستخدم
    final Map<String, List<Keyword>> groupedCurrencies = {};
    for (var curr in currencies) {
      groupedCurrencies.putIfAbsent(curr.type, () => []).add(curr);
    }

    final query = _searchQuery.trim().toLowerCase();

    // فلترة العملات النشطة حسب البحث
    final filteredActiveCodes = groupedCurrencies.keys.where((code) {
      if (query.isEmpty) return true;
      final arabicName = (worldCurrencies[code] ?? '').toLowerCase();
      final codeLower = code.toLowerCase();
      final hasMatchingKeyword = groupedCurrencies[code]!.any((k) => k.word.toLowerCase().contains(query));
      return codeLower.contains(query) || arabicName.contains(query) || hasMatchingKeyword;
    }).toList();

    // عملات قياسية غير نشطة لدى المستخدم ولكنها تطابق البحث (لتفعيلها بنقرة واحدة)
    final List<String> matchingInactiveCodes = [];
    if (query.isNotEmpty) {
      for (final entry in worldCurrencies.entries) {
        final code = entry.key;
        if (!groupedCurrencies.containsKey(code)) {
          final arabicName = entry.value.toLowerCase();
          final codeLower = code.toLowerCase();
          final defSyms = (defaultSymbols[code] ?? []).map((s) => s.toLowerCase());
          if (codeLower.contains(query) || arabicName.contains(query) || defSyms.any((s) => s.contains(query))) {
            matchingInactiveCodes.add(code);
          }
        }
      }
    }

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('إدارة العملات والرموز', style: TextStyle(color: Color(0xFF0F172A), fontWeight: FontWeight.bold, fontSize: 18)),
        centerTitle: true,
        backgroundColor: Colors.white,
        iconTheme: const IconThemeData(color: Color(0xFF0F172A)),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'مزامنة العملات الافتراضية (30 عملة)',
            icon: const Icon(Icons.sync, color: Color(0xFF2563EB)),
            onPressed: _syncDefaults,
          ),
          IconButton(
            tooltip: 'تحديث القائمة',
            icon: const Icon(Icons.refresh, color: Color(0xFF64748B)),
            onPressed: () {
              setState(() => isLoading = true);
              _loadCurrencies(autoSyncIfEmpty: false);
            },
          ),
        ],
      ),
      body: isLoading
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // شريط البحث الذكي
                  Container(
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: Colors.grey.shade300),
                      boxShadow: [
                        BoxShadow(color: Colors.black.withValues(alpha: 0.02), blurRadius: 8, offset: const Offset(0, 2)),
                      ],
                    ),
                    child: TextField(
                      controller: _searchController,
                      textAlign: TextAlign.right,
                      onChanged: (val) {
                        setState(() {
                          _searchQuery = val;
                        });
                      },
                      decoration: InputDecoration(
                        hintText: 'ابحث عن عملة أو رمز (مثل: ين، روبل، ريال، BYN، CNY...)',
                        hintStyle: const TextStyle(color: Color(0xFF94A3B8), fontSize: 13),
                        prefixIcon: const Icon(Icons.search, color: Color(0xFF2563EB)),
                        suffixIcon: _searchQuery.isNotEmpty
                            ? IconButton(
                                icon: const Icon(Icons.clear, size: 18, color: Colors.grey),
                                onPressed: () {
                                  _searchController.clear();
                                  setState(() => _searchQuery = '');
                                },
                              )
                            : null,
                        border: InputBorder.none,
                        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                      ),
                    ),
                  ),

                  const SizedBox(height: 12),

                  // أزرار سريعة: إضافة عملة مخصصة + استعادة/مزامنة الكل
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: _showAddCustomCurrencyDialog,
                          icon: const Icon(Icons.add, size: 16, color: Color(0xFF2563EB)),
                          label: const Text('عملة مخصصة', style: TextStyle(color: Color(0xFF2563EB), fontSize: 12, fontWeight: FontWeight.bold)),
                          style: OutlinedButton.styleFrom(
                            backgroundColor: Colors.white,
                            side: const BorderSide(color: Color(0xFFBFDBFE)),
                            padding: const EdgeInsets.symmetric(vertical: 10),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: _syncDefaults,
                          icon: const Icon(Icons.auto_awesome, size: 16, color: Color(0xFF059669)),
                          label: const Text('مزامنة الـ 30 عملة', style: TextStyle(color: Color(0xFF059669), fontSize: 12, fontWeight: FontWeight.bold)),
                          style: OutlinedButton.styleFrom(
                            backgroundColor: const Color(0xFFECFDF5),
                            side: const BorderSide(color: Color(0xFFA7F3D0)),
                            padding: const EdgeInsets.symmetric(vertical: 10),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 16),

                  // إحصائيات وعناوين
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        'العملات المعتمدة لديك (${filteredActiveCodes.length}):',
                        style: const TextStyle(color: Color(0xFF334155), fontWeight: FontWeight.bold, fontSize: 14),
                      ),
                      Text(
                        'إجمالي الرموز: ${currencies.length}',
                        style: const TextStyle(color: Color(0xFF64748B), fontSize: 12, fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),

                  const SizedBox(height: 10),

                  // قائمة بطاقات العملات
                  Expanded(
                    child: ListView(
                      children: [
                        // في حال البحث عن عملة غير مفعلة، عرض بطاقة تفعيل بنقرة واحدة
                        if (matchingInactiveCodes.isNotEmpty) ...[
                          Container(
                            margin: const EdgeInsets.only(bottom: 12),
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFEFF6FF),
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: const Color(0xFFBFDBFE)),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Row(
                                  children: [
                                    Icon(Icons.lightbulb_outline, size: 16, color: Color(0xFF2563EB)),
                                    SizedBox(width: 6),
                                    Text(
                                      'عملات قياسية متوفرة تطابق بحثك (اضغط لتفعيلها):',
                                      style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold, color: Color(0xFF1E40AF)),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Wrap(
                                  spacing: 8,
                                  runSpacing: 6,
                                  children: matchingInactiveCodes.map((code) {
                                    final name = worldCurrencies[code] ?? code;
                                    return ActionChip(
                                      avatar: const Icon(Icons.add_circle, size: 16, color: Colors.white),
                                      label: Text('$name ($code)', style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.bold)),
                                      backgroundColor: const Color(0xFF2563EB),
                                      onPressed: () => _activateCurrency(code),
                                    );
                                  }).toList(),
                                ),
                              ],
                            ),
                          ),
                        ],

                        if (filteredActiveCodes.isEmpty && matchingInactiveCodes.isEmpty)
                          Container(
                            padding: const EdgeInsets.all(40),
                            alignment: Alignment.center,
                            child: Column(
                              children: [
                                Icon(Icons.search_off, size: 48, color: Colors.grey.shade400),
                                const SizedBox(height: 12),
                                Text(
                                  query.isEmpty ? 'لا توجد عملات مضافة حالياً' : 'لم يتم العثور على عملة أو رمز يطابق "$_searchQuery"',
                                  style: const TextStyle(color: Colors.grey, fontSize: 14),
                                ),
                                if (query.isNotEmpty) ...[
                                  const SizedBox(height: 12),
                                  ElevatedButton.icon(
                                    onPressed: _showAddCustomCurrencyDialog,
                                    icon: const Icon(Icons.add, size: 16, color: Colors.white),
                                    label: const Text('إضافة كعملة مخصصة جديدة', style: TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.bold)),
                                    style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF2563EB)),
                                  ),
                                ],
                              ],
                            ),
                          )
                        else
                          ...filteredActiveCodes.map((code) {
                            final keywords = groupedCurrencies[code] ?? [];
                            final arabicName = worldCurrencies[code] ?? code;
                            final avatarSign = _getCurrencyAvatar(code);

                            return Card(
                              margin: const EdgeInsets.only(bottom: 12),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(14),
                                side: BorderSide(color: Colors.grey.shade200),
                              ),
                              elevation: 0,
                              color: Colors.white,
                              child: Padding(
                                padding: const EdgeInsets.all(14.0),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    // شريط رأس بطاقة العملة
                                    Row(
                                      children: [
                                        CircleAvatar(
                                          backgroundColor: const Color(0xFFEFF6FF),
                                          radius: 18,
                                          child: Text(
                                            avatarSign,
                                            style: const TextStyle(color: Color(0xFF2563EB), fontWeight: FontWeight.bold, fontSize: 13),
                                          ),
                                        ),
                                        const SizedBox(width: 10),
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment: CrossAxisAlignment.start,
                                            children: [
                                              Row(
                                                children: [
                                                  Text(
                                                    arabicName,
                                                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: Color(0xFF0F172A)),
                                                  ),
                                                  const SizedBox(width: 6),
                                                  Container(
                                                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                                    decoration: BoxDecoration(
                                                      color: const Color(0xFFF1F5F9),
                                                      borderRadius: BorderRadius.circular(6),
                                                    ),
                                                    child: Text(
                                                      code,
                                                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 11, color: Color(0xFF475569)),
                                                    ),
                                                  ),
                                                ],
                                              ),
                                              const SizedBox(height: 2),
                                              Text(
                                                '${keywords.length} مرادفات/رموز معرفة',
                                                style: const TextStyle(fontSize: 11, color: Color(0xFF94A3B8)),
                                              ),
                                            ],
                                          ),
                                        ),

                                        // زر إضافة رمز مباشر لهذه العملة
                                        IconButton(
                                          tooltip: 'إضافة رمز لهذه العملة',
                                          icon: const Icon(Icons.add_circle_outline, color: Color(0xFF2563EB), size: 22),
                                          onPressed: () => _showAddSymbolDialog(code),
                                        ),

                                        // قائمة إجراءات العملة
                                        PopupMenuButton<String>(
                                          tooltip: 'خيارات العملة',
                                          icon: const Icon(Icons.more_vert, color: Color(0xFF64748B)),
                                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                                          onSelected: (action) {
                                            if (action == 'add_symbol') {
                                              _showAddSymbolDialog(code);
                                            } else if (action == 'edit_code') {
                                              _showEditCurrencyCodeDialog(code);
                                            } else if (action == 'delete_currency') {
                                              _confirmDeleteEntireCurrency(code, keywords.length);
                                            }
                                          },
                                          itemBuilder: (context) => [
                                            const PopupMenuItem(
                                              value: 'add_symbol',
                                              child: Row(
                                                children: [
                                                  Icon(Icons.add, color: Color(0xFF2563EB), size: 18),
                                                  SizedBox(width: 8),
                                                  Text('إضافة رمز للعملة', style: TextStyle(fontSize: 13)),
                                                ],
                                              ),
                                            ),
                                            const PopupMenuItem(
                                              value: 'edit_code',
                                              child: Row(
                                                children: [
                                                  Icon(Icons.edit_note, color: Color(0xFF475569), size: 18),
                                                  SizedBox(width: 8),
                                                  Text('تعديل كود العملة', style: TextStyle(fontSize: 13)),
                                                ],
                                              ),
                                            ),
                                            const PopupMenuDivider(),
                                            const PopupMenuItem(
                                              value: 'delete_currency',
                                              child: Row(
                                                children: [
                                                  Icon(Icons.delete_forever, color: Color(0xFFDC2626), size: 18),
                                                  SizedBox(width: 8),
                                                  Text('حذف العملة بالكامل', style: TextStyle(color: Color(0xFFDC2626), fontSize: 13)),
                                                ],
                                              ),
                                            ),
                                          ],
                                        ),
                                      ],
                                    ),

                                    const Divider(height: 18, thickness: 0.7),

                                    // قائمة الرموز المرادفة للعملة
                                    Wrap(
                                      spacing: 8,
                                      runSpacing: 8,
                                      children: [
                                        ...keywords.map((k) {
                                          return InkWell(
                                            onTap: () => _showEditSymbolDialog(k),
                                            borderRadius: BorderRadius.circular(8),
                                            child: Chip(
                                              label: Row(
                                                mainAxisSize: MainAxisSize.min,
                                                children: [
                                                  Text(k.word, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                                                  const SizedBox(width: 4),
                                                  const Icon(Icons.edit, size: 11, color: Color(0xFF94A3B8)),
                                                ],
                                              ),
                                              deleteIcon: const Icon(Icons.close, size: 14, color: Color(0xFFEF4444)),
                                              onDeleted: () => _deleteSymbol(k.id, k.word),
                                              backgroundColor: const Color(0xFFF8FAFC),
                                              shape: RoundedRectangleBorder(
                                                borderRadius: BorderRadius.circular(8),
                                                side: BorderSide(color: Colors.grey.shade300),
                                              ),
                                            ),
                                          );
                                        }),

                                        // زر سريع لإضافة رمز إضافي كـ ActionChip
                                        ActionChip(
                                          onPressed: () => _showAddSymbolDialog(code),
                                          avatar: const Icon(Icons.add, size: 14, color: Color(0xFF2563EB)),
                                          label: const Text('رمز جديد', style: TextStyle(fontSize: 11, color: Color(0xFF2563EB), fontWeight: FontWeight.bold)),
                                          backgroundColor: const Color(0xFFEFF6FF),
                                          shape: RoundedRectangleBorder(
                                            borderRadius: BorderRadius.circular(8),
                                            side: const BorderSide(color: Color(0xFFBFDBFE)),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }
}