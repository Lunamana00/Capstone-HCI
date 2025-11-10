package com.example.imu_reading

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import android.util.Log
import com.google.android.gms.wearable.Wearable
import com.google.android.gms.tasks.Tasks

class MainActivity: FlutterActivity() {
    private val CHANNEL = "com.example.imu_reading/wear"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        
        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            CHANNEL
        ).setMethodCallHandler { call, result ->
            if (call.method == "vibrate") {
                val forceJson = call.argument<String>("data")
                if (forceJson != null) {
                    sendVibrationToWatch(forceJson)
                    result.success("Message sent to watch")
                } else {
                    result.error("INVALID_ARGUMENT", "Data was null", null)
                }
            } else {
                result.notImplemented()
            }
        }
    }

    private fun sendVibrationToWatch(jsonData: String) {
        Log.d("MainActivity", "Sending vibration to watch: $jsonData")
        
        val messageClient = Wearable.getMessageClient(this)
        
        try {
            val nodeListTask = Wearable.getNodeClient(this).connectedNodes
            val nodes = Tasks.await(nodeListTask)
            
            Log.d("MainActivity", "Connected nodes: ${nodes.size}")
            
            for (node in nodes) {
                Log.d("MainActivity", "Sending to node: ${node.displayName} (${node.id})")
                
                val sendTask = messageClient.sendMessage(
                    node.id,
                    "/VIBRATE_TAIL",
                    jsonData.toByteArray(Charsets.UTF_8)
                )
                
                Tasks.await(sendTask)
                Log.d("MainActivity", "✅ Message sent successfully to ${node.displayName}")
            }
            
            if (nodes.isEmpty()) {
                Log.w("MainActivity", "⚠️ No connected Wear OS devices found")
            }
        } catch (e: Exception) {
            Log.e("MainActivity", "❌ Error sending message to watch", e)
        }
    }
}
