class Keyword {
  final int id;
  final int userId;
  final String word;
  final String type;
  final DateTime createdAt;

  Keyword({
    required this.id,
    required this.userId,
    required this.word,
    required this.type,
    required this.createdAt,
  });

  factory Keyword.fromJson(Map<String, dynamic> json) {
    return Keyword(
      id: json['id'],
      userId: json['userId'],
      word: json['word'],
      type: json['type'],
      createdAt: DateTime.parse(json['createdAt']),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'userId': userId,
      'word': word,
      'type': type,
      'createdAt': createdAt.toIso8601String(),
    };
  }
}
