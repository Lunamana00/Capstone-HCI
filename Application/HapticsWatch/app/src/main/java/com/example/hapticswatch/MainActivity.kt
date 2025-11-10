package com.example.hapticswatch

import android.app.Activity
import android.content.Context
import android.os.Build
import android.os.Bundle
import android.os.VibrationEffect
import android.os.Vibrator
import android.util.Log
import android.view.WindowManager // 💡 Wake Lock (화면 켜짐)
import android.widget.TextView
import com.example.hapticswatch.R // 💡 레이아웃(R) import
import com.google.android.gms.wearable.MessageClient
import com.google.android.gms.wearable.MessageEvent
import com.google.android.gms.wearable.Wearable
import org.json.JSONObject

// 💡 1. 서비스/리시버 대신 Activity가 직접 메시지 리스너를 구현
class MainActivity : Activity(), MessageClient.OnMessageReceivedListener {

    private lateinit var debugTextView: TextView
    private lateinit var vibrator: Vibrator

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // 2. 💡 UI 레이아웃 설정
        setContentView(R.layout.activity_main)

        // 3. 💡 앱이 켜져 있는 동안 화면이 꺼지지 않도록 설정 (핵심)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        debugTextView = findViewById(R.id.debug_text_view)
        vibrator = getSystemService(Context.VIBRATOR_SERVICE) as Vibrator

        Log.d("HapticsWatch", "onCreate() - VIBRATOR READY")
    }

    // 4. 💡 폰에서 보낸 메시지를 '직접' 수신하는 함수
    override fun onMessageReceived(messageEvent: MessageEvent) {
        if (messageEvent.path == "/VIBRATE_TAIL") {
            val jsonString = String(messageEvent.data, Charsets.UTF_8)
            val force = JSONObject(jsonString).optDouble("force", 1.0).toFloat()

            Log.d("HapticsWatch", "Message Received! Force: $force")

            // 5. 💡 UI 스레드에서 텍스트 업데이트 및 진동 실행
            runOnUiThread {
                debugTextView.text = "Received Force:\n${"%.2f".format(force)}"
                triggerVibration(force)
            }
        }
    }

    // 6. 💡 앱이 '켜질 때' (화면에 나타날 때)
    override fun onResume() {
        super.onResume()
        // 💡 충돌 위험이 있는 registerReceiver() 대신, MessageClient.addListener() 사용
        Wearable.getMessageClient(this).addListener(this)

        debugTextView.text = "Listening... (Ready)"
        Log.d("HapticsWatch", "onResume() - Listener ADDED")
    }

    // 7. 💡 앱이 '꺼질 때'
    override fun onPause() {
        super.onPause()
        Wearable.getMessageClient(this).removeListener(this)

        debugTextView.text = "Paused. (Not Listening)"
        Log.d("HapticsWatch", "onPause() - Listener REMOVED")
    }

    // 8. 💡 진동 실행 함수
    private fun triggerVibration(force: Float) {
        val durationMs = (force * 200).coerceIn(100.0f, 2000.0f).toLong()
        val amplitude = (force * 50).coerceIn(50.0f, 255.0f).toInt()

        // 👇 디버깅 로그 추가
        Log.d("HapticsWatch", "Attempting to Vibrate: Duration=$durationMs, Amplitude=$amplitude")

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val effect = VibrationEffect.createOneShot(durationMs, amplitude)
            vibrator.vibrate(effect)
        } else {
            vibrator.vibrate(durationMs)
        }
    }
}