import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../services/api_service.dart';
import 'operations_provider.dart';

class MonitoredNumber {
  final int id;
  final String phoneNumber;
  final String contactName;
  final bool isActive;

  MonitoredNumber({
    required this.id,
    required this.phoneNumber,
    required this.contactName,
    required this.isActive,
  });

  factory MonitoredNumber.fromJson(Map<String, dynamic> json) {
    return MonitoredNumber(
      id: json['id'],
      phoneNumber: json['phoneNumber'] ?? '',
      contactName: json['contactName'] ?? '',
      isActive: json['isActive'] ?? true,
    );
  }
}

class MonitoredNumbersState {
  final bool isLoading;
  final List<MonitoredNumber> numbers;
  final String? error;

  MonitoredNumbersState({
    this.isLoading = false,
    this.numbers = const [],
    this.error,
  });

  MonitoredNumbersState copyWith({
    bool? isLoading,
    List<MonitoredNumber>? numbers,
    String? error,
  }) {
    return MonitoredNumbersState(
      isLoading: isLoading ?? this.isLoading,
      numbers: numbers ?? this.numbers,
      error: error,
    );
  }
}

class MonitoredNumbersNotifier extends Notifier<MonitoredNumbersState> {
  ApiService get _apiService => ref.read(apiServiceProvider);

  @override
  MonitoredNumbersState build() {
    Future.microtask(() => fetchNumbers());
    return MonitoredNumbersState();
  }

  Future<void> fetchNumbers() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final data = await _apiService.getMonitoredNumbers();
      final numbers = data.map((json) => MonitoredNumber.fromJson(json)).toList();
      state = state.copyWith(isLoading: false, numbers: numbers);
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  Future<void> addNumber(String phone, String name) async {
    try {
      await _apiService.addMonitoredNumber(phone, name);
      await fetchNumbers(); // Refresh list after adding
    } catch (e) {
      rethrow;
    }
  }

  Future<void> editNumber(int id, String name) async {
    try {
      await _apiService.editMonitoredNumber(id, name);
      await fetchNumbers(); // Refresh list after editing
    } catch (e) {
      rethrow;
    }
  }

  Future<void> deleteNumber(int id) async {
    try {
      await _apiService.deleteMonitoredNumber(id);
      await fetchNumbers(); // Refresh list after deleting
    } catch (e) {
      rethrow;
    }
  }

  Future<void> toggleStatus(int id) async {
    final index = state.numbers.indexWhere((n) => n.id == id);
    if (index == -1) return;

    final current = state.numbers[index];
    final newStatus = !current.isActive;

    // 1. Optimistic update (instant visual feedback)
    final updatedList = List<MonitoredNumber>.from(state.numbers);
    updatedList[index] = MonitoredNumber(
      id: current.id,
      phoneNumber: current.phoneNumber,
      contactName: current.contactName,
      isActive: newStatus,
    );
    state = state.copyWith(numbers: updatedList);

    // 2. Persist to API
    try {
      await _apiService.toggleMonitoredNumberStatus(id, isActive: newStatus);
    } catch (e) {
      // Revert on error
      await fetchNumbers();
      rethrow;
    }
  }
}

final monitoredNumbersProvider = NotifierProvider.autoDispose<MonitoredNumbersNotifier, MonitoredNumbersState>(() {
  return MonitoredNumbersNotifier();
});
