import 'package:flutter/material.dart';
import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:sensors_plus/sensors_plus.dart';
import 'package:http/http.dart' as http;
import 'package:flutter/services.dart';
import 'package:shelf/shelf.dart' as shelf;
import 'package:shelf/shelf_io.dart' as io;
import 'package:shelf_router/shelf_router.dart' as router;
import 'package:network_info_plus/network_info_plus.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Flutter IMU & Haptic App',
      theme: ThemeData(colorSchemeSeed: Colors.blue, useMaterial3: true),
      home: const SensorPage(),
    );
  }
}

class SensorPage extends StatefulWidget {
  const SensorPage({super.key});

  @override
  State<SensorPage> createState() => _SensorPageState();
}

class _SensorPageState extends State<SensorPage> {
  // --- 네이티브 채널 및 서버 관련 변수 ---
  static const _wearChannel = MethodChannel('com.example.imu_reading/wear');
  String _phoneIp = "Loading...";
  HttpServer? _vibrationServer;
  final int _vibrationPort = 8081;

  // --- IMU 전송 관련 변수 ---
  String _pcServerUrl = "http://127.0.0.1:928";
  final TextEditingController _ipController = TextEditingController();
  final int _sendIntervalMs = 50;
  final Duration _sensorInterval = const Duration(milliseconds: 50);
  
  List<double> _accelValues = [0, 0, 0];
  List<double> _gyroValues = [0, 0, 0];
  String _status = "Initializing...";
  bool _isSending = false;
  
  Timer? _sendTimer;
  Timer? _uiTimer;
  StreamSubscription<AccelerometerEvent>? _accelSubscription;
  StreamSubscription<GyroscopeEvent>? _gyroSubscription;

  @override
  void initState() {
    super.initState();
    _ipController.text = "192.168.137.1"; // PC IP 기본값
    _setPCServerUrl();
    
    // 센서 및 타이머 시작
    startSensorListeners();
    startSendingTimer();
    startUiTimer();
    
    // 폰 IP 및 서버 시작
    _getPhoneIp();
    _startVibrationServer();
  }

  // 1. 폰의 Wi-Fi IP 주소 가져오기
  Future<void> _getPhoneIp() async {
    final info = NetworkInfo();
    String? ip = await info.getWifiIP();
    if (mounted) {
      setState(() {
        _phoneIp = ip ?? "No Wi-Fi IP found";
      });
    }
  }

  // 2. 충돌 수신 서버 시작 (Unity -> Phone -> Watch)
  Future<void> _startVibrationServer() async {
    var app = router.Router();
    
    app.post('/vibrate', (shelf.Request request) async {
      final jsonBody = await request.readAsString();
      
      if (mounted) {
        setState(() {
          _status = "Collision Data Received! $jsonBody";
        });
      }
      
      debugPrint("[Server] Vibration data received: $jsonBody");
      
      try {
        await _wearChannel.invokeMethod('vibrate', {'data': jsonBody});
        
        if (mounted) {
          setState(() {
            _status = "Data sent to Watch. $jsonBody";
          });
        }
        
        return shelf.Response.ok('{"status":"ok"}');
      } catch (e) {
        if (mounted) {
          setState(() {
            _status = "ERROR sending to Watch: $e";
          });
        }
        
        debugPrint("Failed to invoke native method: $e");
        return shelf.Response.internalServerError(
          body: 'Failed to send to watch',
        );
      }
    });

    try {
      _vibrationServer = await io.serve(app, '0.0.0.0', _vibrationPort);
      debugPrint('Vibration server started on port $_vibrationPort');
    } catch (e) {
      debugPrint('Error starting server: $e');
      if (mounted) {
        setState(() {
          _status = "ERROR starting server: $e";
        });
      }
    }
  }

  // 3. PC 서버 URL 설정
  void _setPCServerUrl() {
    String ip = _ipController.text.trim();
    if (ip.isEmpty) {
      setState(() {
        _status = "Status: PC IP를 입력하세요.";
      });
      return;
    }
    
    setState(() {
      _pcServerUrl = "http://$ip:928";
      _status = "Status: PC Target IP set to $ip";
    });
  }

  // 4. IMU 센서 리스너 시작
  void startSensorListeners() {
    debugPrint('[Sensor] Starting sensor listeners...');
    
    // 가속도계 리스너
    _accelSubscription = accelerometerEventStream(
      samplingPeriod: _sensorInterval,
    ).listen(
      (AccelerometerEvent event) {
        if (mounted) {
          _accelValues = [event.x, event.y, event.z];
        }
      },
      onError: (error) {
        debugPrint('[Sensor] Accelerometer Error: $error');
        if (mounted) {
          setState(() {
            _status = 'Accelerometer Error: $error';
          });
        }
      },
      cancelOnError: false,
    );

    // 자이로스코프 리스너
    _gyroSubscription = gyroscopeEventStream(
      samplingPeriod: _sensorInterval,
    ).listen(
      (GyroscopeEvent event) {
        if (mounted) {
          _gyroValues = [event.x, event.y, event.z];
        }
      },
      onError: (error) {
        debugPrint('[Sensor] Gyroscope Error: $error');
        if (mounted) {
          setState(() {
            _status = 'Gyroscope Error: $error';
          });
        }
      },
      cancelOnError: false,
    );

    debugPrint('[Sensor] Sensor listeners started successfully');
  }

  // 5. IMU 데이터 전송
  Future<void> sendImuData() async {
    try {
      final Map<String, dynamic> jsonData = {
        'timestamp': DateTime.now().millisecondsSinceEpoch,
        'accelerometer': {
          'x': _accelValues[0],
          'y': _accelValues[1],
          'z': _accelValues[2],
        },
        'gyroscope': {
          'x': _gyroValues[0],
          'y': _gyroValues[1],
          'z': _gyroValues[2],
        },
      };

      final response = await http.post(
        Uri.parse(_pcServerUrl),
        headers: {'Content-Type': 'application/json; charset=UTF-8'},
        body: jsonEncode(jsonData),
      ).timeout(const Duration(milliseconds: 100));

      if (mounted && _isSending) {
        if (response.statusCode == 200) {
          setState(() {
            _status = "Status: IMU Data sent";
          });
        } else {
          setState(() {
            _status = "Status: IMU Error ${response.statusCode}";
          });
        }
      }
    } catch (e) {
      if (mounted && _isSending) {
        setState(() {
          _status = "Status: IMU Network Error (Check PC IP)";
        });
      }
    }
  }

  // 6. IMU 전송 타이머
  void startSendingTimer() {
    _sendTimer = Timer.periodic(
      Duration(milliseconds: _sendIntervalMs),
      (timer) {
        if (_isSending) {
          sendImuData();
        }
      },
    );
  }

  // 7. UI 업데이트 타이머
  void startUiTimer() {
    _uiTimer = Timer.periodic(
      const Duration(milliseconds: 100),
      (timer) {
        if (mounted) {
          setState(() {
            // UI 갱신 (센서 값 표시)
          });
        }
      },
    );
  }

  // 8. UI 빌드
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text("IMU & Haptic App"),
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
      ),
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // --- 폰 서버 정보 (Unity용) ---
              const Text(
                "Phone Server (for Unity Collision)",
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                "Unity의 TailCollisionSender가 이 주소로 데이터를 보내야 합니다:",
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: 4),
              Text(
                "http://$_phoneIp:$_vibrationPort",
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: Colors.green,
                ),
              ),
              const Divider(height: 24),

              // --- PC 서버 설정 (IMU 전송용) ---
              const Text(
                "PC Server (for IMU Data)",
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _ipController,
                      decoration: const InputDecoration(
                        labelText: "PC IP Address",
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  ElevatedButton(
                    onPressed: () {
                      _setPCServerUrl();
                      FocusScope.of(context).unfocus();
                    },
                    child: const Text("Set"),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text("Current IMU Target: $_pcServerUrl"),
              const Divider(height: 24),

              // --- IMU 전송 스위치 ---
              SwitchListTile(
                title: const Text('IMU 데이터 PC로 전송'),
                subtitle: Text(_isSending ? '전송 중...' : '일시정지'),
                value: _isSending,
                onChanged: (bool value) {
                  setState(() {
                    _isSending = value;
                    _status = _isSending
                        ? "IMU Sending Enabled"
                        : "IMU Sending Paused";
                  });
                },
              ),
              const Divider(),
              const SizedBox(height: 16),

              // --- 상태 표시 ---
              Text(
                _status,
                style: const TextStyle(
                  fontStyle: FontStyle.italic,
                  color: Colors.blue,
                ),
              ),
              const SizedBox(height: 16),

              // --- Live IMU 데이터 표시 ---
              const Text(
                'Live IMU Data:',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                'Accel: (${_accelValues[0].toStringAsFixed(2)}, '
                '${_accelValues[1].toStringAsFixed(2)}, '
                '${_accelValues[2].toStringAsFixed(2)})',
              ),
              Text(
                'Gyro: (${_gyroValues[0].toStringAsFixed(2)}, '
                '${_gyroValues[1].toStringAsFixed(2)}, '
                '${_gyroValues[2].toStringAsFixed(2)})',
              ),
            ],
          ),
        ),
      ),
    );
  }

  // 9. 리소스 해제
  @override
  void dispose() {
    _vibrationServer?.close(force: true);
    _ipController.dispose();
    _accelSubscription?.cancel();
    _gyroSubscription?.cancel();
    _sendTimer?.cancel();
    _uiTimer?.cancel();
    super.dispose();
  }
}
