class CountryCode {
  final String name;
  final String code; // e.g. "218"
  final String dialCode; // e.g. "+218"
  final String flag; // e.g. "🇱🇾"
  final String example;

  const CountryCode({
    required this.name,
    required this.code,
    required this.dialCode,
    required this.flag,
    required this.example,
  });

  static const List<CountryCode> supportedCountries = [
    CountryCode(name: 'ليبيا', code: '218', dialCode: '+218', flag: '🇱🇾', example: '912345678'),
    CountryCode(name: 'مصر', code: '20', dialCode: '+20', flag: '🇪🇬', example: '1012345678'),
    CountryCode(name: 'تركيا', code: '90', dialCode: '+90', flag: '🇹🇷', example: '5321234567'),
    CountryCode(name: 'تونس', code: '216', dialCode: '+216', flag: '🇹🇳', example: '98123456'),
    CountryCode(name: 'الجزائر', code: '213', dialCode: '+213', flag: '🇩🇿', example: '551234567'),
    CountryCode(name: 'المغرب', code: '212', dialCode: '+212', flag: '🇲🇦', example: '612345678'),
    CountryCode(name: 'السعودية', code: '966', dialCode: '+966', flag: '🇸🇦', example: '501234567'),
    CountryCode(name: 'الإمارات', code: '971', dialCode: '+971', flag: '🇦🇪', example: '501234567'),
    CountryCode(name: 'الأردن', code: '962', dialCode: '+962', flag: '🇯🇴', example: '791234567'),
    CountryCode(name: 'سوريا', code: '963', dialCode: '+963', flag: '🇸🇾', example: '944123456'),
    CountryCode(name: 'العراق', code: '964', dialCode: '+964', flag: '🇮🇶', example: '7701234567'),
    CountryCode(name: 'لبنان', code: '961', dialCode: '+961', flag: '🇱🇧', example: '70123456'),
    CountryCode(name: 'فلسطين', code: '970', dialCode: '+970', flag: '🇵🇸', example: '599123456'),
    CountryCode(name: 'السودان', code: '249', dialCode: '+249', flag: '🇸🇩', example: '912345678'),
    CountryCode(name: 'الكويت', code: '965', dialCode: '+965', flag: '🇰🇼', example: '99123456'),
    CountryCode(name: 'قطر', code: '974', dialCode: '+974', flag: '🇶🇦', example: '55123456'),
    CountryCode(name: 'عُمان', code: '968', dialCode: '+968', flag: '🇴🇲', example: '91234567'),
    CountryCode(name: 'البحرين', code: '973', dialCode: '+973', flag: '🇧🇭', example: '39123456'),
    CountryCode(name: 'اليمن', code: '967', dialCode: '+967', flag: '🇾🇪', example: '771234567'),
    CountryCode(name: 'بريطانيا', code: '44', dialCode: '+44', flag: '🇬🇧', example: '7911123456'),
    CountryCode(name: 'الولايات المتحدة', code: '1', dialCode: '+1', flag: '🇺🇸', example: '2025550123'),
  ];

  static CountryCode get defaultCountry => supportedCountries.first; // Libya (+218)

  static CountryCode findByCode(String code) {
    return supportedCountries.firstWhere(
      (c) => c.code == code,
      orElse: () => defaultCountry,
    );
  }

  static CountryCode parseFromFullPhone(String fullPhone) {
    final clean = fullPhone.replaceAll(RegExp(r'[^\d]'), '');
    if (clean.isEmpty) return defaultCountry;
    
    for (var c in supportedCountries) {
      if (clean.startsWith(c.code)) {
        return c;
      }
    }
    return defaultCountry;
  }
}