class SubscriptionStatus {
  final String status; // None, Trial, Active, Grace, Expired
  final String? planId;
  final String? planPriceId;
  final int? billingCycle;
  final String planType;
  final bool isTrial;
  final DateTime? startedAt;
  final DateTime? expiresAt;
  final DateTime? graceUntil;
  final int daysRemaining;

  SubscriptionStatus({
    required this.status,
    this.planId,
    this.planPriceId,
    this.billingCycle,
    required this.planType,
    required this.isTrial,
    this.startedAt,
    this.expiresAt,
    this.graceUntil,
    required this.daysRemaining,
  });

  factory SubscriptionStatus.fromJson(Map<String, dynamic> json) {
    return SubscriptionStatus(
      status: json['status'] ?? 'None',
      planId: json['planId']?.toString(),
      planPriceId: json['planPriceId']?.toString(),
      billingCycle: json['billingCycle'] is int ? json['billingCycle'] : null,
      planType: json['planType'] ?? 'لا يوجد اشتراك',
      isTrial: json['isTrial'] == true,
      startedAt: json['startedAt'] != null ? DateTime.tryParse(json['startedAt'].toString()) : null,
      expiresAt: json['expiresAt'] != null ? DateTime.tryParse(json['expiresAt'].toString()) : null,
      graceUntil: json['graceUntil'] != null ? DateTime.tryParse(json['graceUntil'].toString()) : null,
      daysRemaining: json['daysRemaining'] is int ? json['daysRemaining'] : 0,
    );
  }

  bool get isActive => status == 'Active' || status == 'Trial';
  bool get isGrace => status == 'Grace';
  bool get isExpired => status == 'Expired';
}

class PlanPriceItem {
  final String id;
  final double price;
  final int durationDays;

  PlanPriceItem({
    required this.id,
    required this.price,
    required this.durationDays,
  });

  factory PlanPriceItem.fromJson(Map<String, dynamic> json) {
    return PlanPriceItem(
      id: json['id']?.toString() ?? '',
      price: (json['price'] as num?)?.toDouble() ?? 0.0,
      durationDays: json['durationDays'] is int ? json['durationDays'] : 30,
    );
  }
}

class SubscriptionPlanItem {
  final String id;
  final String name;
  final String nameAr;
  final String? descriptionAr;
  final String themeKey;
  final bool isPopular;
  final int sortOrder;
  final List<String> features;
  final PlanPriceItem? monthlyPrice;
  final PlanPriceItem? quarterlyPrice;
  final PlanPriceItem? semiAnnualPrice;
  final PlanPriceItem? yearlyPrice;
  final int? quarterlySaving;
  final int? semiAnnualSaving;
  final int? yearlySaving;

  SubscriptionPlanItem({
    required this.id,
    required this.name,
    required this.nameAr,
    this.descriptionAr,
    required this.themeKey,
    required this.isPopular,
    required this.sortOrder,
    required this.features,
    this.monthlyPrice,
    this.quarterlyPrice,
    this.semiAnnualPrice,
    this.yearlyPrice,
    this.quarterlySaving,
    this.semiAnnualSaving,
    this.yearlySaving,
  });

  factory SubscriptionPlanItem.fromJson(Map<String, dynamic> json) {
    final prices = json['prices'] as Map<String, dynamic>? ?? {};
    final savings = json['savings'] as Map<String, dynamic>? ?? {};

    return SubscriptionPlanItem(
      id: json['id']?.toString() ?? '',
      name: json['name'] ?? '',
      nameAr: json['nameAr'] ?? '',
      descriptionAr: json['descriptionAr'],
      themeKey: json['themeKey'] ?? 'blue',
      isPopular: json['isPopular'] == true,
      sortOrder: json['sortOrder'] is int ? json['sortOrder'] : 0,
      features: (json['features'] as List<dynamic>?)?.map((f) => f.toString()).toList() ?? [],
      monthlyPrice: prices['monthly'] != null ? PlanPriceItem.fromJson(prices['monthly']) : null,
      quarterlyPrice: prices['quarterly'] != null ? PlanPriceItem.fromJson(prices['quarterly']) : null,
      semiAnnualPrice: prices['semiAnnual'] != null ? PlanPriceItem.fromJson(prices['semiAnnual']) : null,
      yearlyPrice: prices['yearly'] != null ? PlanPriceItem.fromJson(prices['yearly']) : null,
      quarterlySaving: savings['quarterlySavingPercent'] as int?,
      semiAnnualSaving: savings['semiAnnualSavingPercent'] as int?,
      yearlySaving: savings['yearlySavingPercent'] as int?,
    );
  }

  PlanPriceItem? getPriceForCycle(int cycle) {
    switch (cycle) {
      case 1: return monthlyPrice;
      case 2: return quarterlyPrice;
      case 3: return semiAnnualPrice;
      case 4: return yearlyPrice;
      default: return monthlyPrice;
    }
  }

  int? getSavingForCycle(int cycle) {
    switch (cycle) {
      case 2: return quarterlySaving;
      case 3: return semiAnnualSaving;
      case 4: return yearlySaving;
      default: return null;
    }
  }
}

class PlanChangePreview {
  final int kind; // 1 = Renewal, 2 = Change, 3 = NewStart
  final bool requiresConfirmation;
  final String? currentPlanNameAr;
  final int? currentBillingCycle;
  final DateTime? currentExpiresAt;
  final int remainingDays;
  final String newPlanNameAr;
  final int newBillingCycle;
  final DateTime newStartsAt;
  final DateTime newExpiresAt;

  PlanChangePreview({
    required this.kind,
    required this.requiresConfirmation,
    this.currentPlanNameAr,
    this.currentBillingCycle,
    this.currentExpiresAt,
    required this.remainingDays,
    required this.newPlanNameAr,
    required this.newBillingCycle,
    required this.newStartsAt,
    required this.newExpiresAt,
  });

  factory PlanChangePreview.fromJson(Map<String, dynamic> json) {
    return PlanChangePreview(
      kind: json['kind'] is int ? json['kind'] : 3,
      requiresConfirmation: json['requiresConfirmation'] == true,
      currentPlanNameAr: json['currentPlanNameAr'],
      currentBillingCycle: json['currentBillingCycle'] as int?,
      currentExpiresAt: json['currentExpiresAt'] != null ? DateTime.tryParse(json['currentExpiresAt'].toString()) : null,
      remainingDays: json['remainingDays'] is int ? json['remainingDays'] : 0,
      newPlanNameAr: json['newPlanNameAr'] ?? '',
      newBillingCycle: json['newBillingCycle'] is int ? json['newBillingCycle'] : 1,
      newStartsAt: DateTime.tryParse(json['newStartsAt'].toString()) ?? DateTime.now(),
      newExpiresAt: DateTime.tryParse(json['newExpiresAt'].toString()) ?? DateTime.now(),
    );
  }
}

class DiscountValidationResult {
  final String code;
  final double originalPrice;
  final double discountAmount;
  final double finalPrice;
  final int discountType;
  final double value;

  DiscountValidationResult({
    required this.code,
    required this.originalPrice,
    required this.discountAmount,
    required this.finalPrice,
    required this.discountType,
    required this.value,
  });

  factory DiscountValidationResult.fromJson(Map<String, dynamic> json) {
    return DiscountValidationResult(
      code: json['code'] ?? '',
      originalPrice: (json['originalPrice'] as num?)?.toDouble() ?? 0.0,
      discountAmount: (json['discountAmount'] as num?)?.toDouble() ?? 0.0,
      finalPrice: (json['finalPrice'] as num?)?.toDouble() ?? 0.0,
      discountType: json['discountType'] is int ? json['discountType'] : 0,
      value: (json['value'] as num?)?.toDouble() ?? 0.0,
    );
  }
}
