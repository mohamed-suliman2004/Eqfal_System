import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/operation.dart';
import '../models/keyword.dart';
import '../services/api_service.dart';

final apiServiceProvider = Provider<ApiService>((ref) {
  return ApiService();
});

final operationsProvider = FutureProvider<List<Operation>>((ref) async {
  final apiService = ref.read(apiServiceProvider);
  return apiService.getOperations();
});

final keywordsProvider = FutureProvider<List<Keyword>>((ref) async {
  final apiService = ref.read(apiServiceProvider);
  final data = await apiService.getKeywords('');
  return data.map((json) => Keyword.fromJson(json)).toList();
});
