class Operation {
  final int id;
  final int userId;
  final int? monitoredNumberId;
  final String? senderNumber;
  final String? receiverNumber;
  final String? category;
  final double? amount;
  final String? currency;
  final String? party;
  final String? source;
  final String? notes;
  final String status;
  final String? rawMessage;
  final bool isReviewed;
  final bool isOutgoing;
  final DateTime createdAt;

  Operation({
    required this.id,
    required this.userId,
    this.monitoredNumberId,
    this.senderNumber,
    this.receiverNumber,
    this.category,
    this.amount,
    this.currency,
    this.party,
    this.source,
    this.notes,
    required this.status,
    this.rawMessage,
    this.isReviewed = false,
    this.isOutgoing = false,
    required this.createdAt,
  });

  factory Operation.fromJson(Map<String, dynamic> json) {
    return Operation(
      id: json['id'],
      userId: json['userId'] ?? 1,
      monitoredNumberId: json['monitoredNumberId'],
      senderNumber: json['senderNumber'],
      receiverNumber: json['receiverNumber'],
      category: json['category'],
      amount: json['amount'] != null ? (json['amount'] as num).toDouble() : null,
      currency: json['currency'],
      party: json['party'],
      source: json['source'],
      notes: json['notes'],
      status: json['status'],
      rawMessage: json['rawMessage'],
      isReviewed: json['isReviewed'] ?? false,
      isOutgoing: json['isOutgoing'] ?? false,
      createdAt: DateTime.parse(json['createdAt']),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'userId': userId,
      'monitoredNumberId': monitoredNumberId,
      'senderNumber': senderNumber,
      'receiverNumber': receiverNumber,
      'category': category,
      'amount': amount,
      'currency': currency,
      'party': party,
      'source': source,
      'notes': notes,
      'status': status,
      'rawMessage': rawMessage,
      'isReviewed': isReviewed,
      'isOutgoing': isOutgoing,
      'createdAt': createdAt.toIso8601String(),
    };
  }

  Operation copyWith({
    int? id,
    int? userId,
    int? monitoredNumberId,
    String? senderNumber,
    String? receiverNumber,
    String? category,
    double? amount,
    String? currency,
    String? party,
    String? source,
    String? notes,
    String? status,
    String? rawMessage,
    bool? isReviewed,
    bool? isOutgoing,
    DateTime? createdAt,
  }) {
    return Operation(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      monitoredNumberId: monitoredNumberId ?? this.monitoredNumberId,
      senderNumber: senderNumber ?? this.senderNumber,
      receiverNumber: receiverNumber ?? this.receiverNumber,
      category: category ?? this.category,
      amount: amount ?? this.amount,
      currency: currency ?? this.currency,
      party: party ?? this.party,
      source: source ?? this.source,
      notes: notes ?? this.notes,
      status: status ?? this.status,
      rawMessage: rawMessage ?? this.rawMessage,
      isReviewed: isReviewed ?? this.isReviewed,
      isOutgoing: isOutgoing ?? this.isOutgoing,
      createdAt: createdAt ?? this.createdAt,
    );
  }

  bool get isDeleted =>
      status == 'محذوف' ||
      status == 'محذوفة' ||
      status == 'deleted' ||
      status == 'revoked';

  bool get isEdited => notes != null && (notes!.contains('تعديل') || notes!.contains('✏️') || notes!.contains('⚠️'));

  String get cleanRawMessage {
    if (rawMessage == null) return '';
    return rawMessage!.replaceFirst(RegExp(r'^\s*\[MSG_ID:[^\]]+\]\s*'), '').trim();
  }
}
