package com.fit3bridge.prototype

import android.os.Bundle
import android.text.InputType
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.PermissionController
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.time.Duration
import java.time.Instant
import java.util.LinkedHashSet
import java.util.concurrent.atomic.AtomicLong

class MainActivity : ComponentActivity() {

    private lateinit var pcIpEdit: EditText
    private lateinit var pcPortEdit: EditText
    private lateinit var statusText: TextView
    private lateinit var lastSampleText: TextView

    private val appScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var streamingJob: Job? = null

    private val seq = AtomicLong(0)
    private val sentSampleKeys = LinkedHashSet<String>()

    private val permissions = setOf(
        HealthPermission.getReadPermission(HeartRateRecord::class)
    )

    private val requestPermissions =
        registerForActivityResult(
            PermissionController.createRequestPermissionResultContract()
        ) { granted ->
            if (granted.containsAll(permissions)) {
                setStatus("Health Connect permission granted.")
            } else {
                setStatus("Heart-rate permission was not granted.")
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        buildUi()
    }

    private fun buildUi() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(40, 40, 40, 40)
        }

        val title = TextView(this).apply {
            text = "Fit3 Health Bridge"
            textSize = 24f
        }

        val explanation = TextView(this).apply {
            text = """
                This app reads heart-rate samples from Health Connect and sends them over local Wi-Fi.

                Recommended for PC VR / Quest Link: start the PC bridge, then enter the PC IPv4 address and port 7777.
                For standalone Quest: enter the Quest IPv4 address and Unity port 5055.
            """.trimIndent()
            textSize = 16f
            setPadding(0, 20, 0, 20)
        }

        pcIpEdit = EditText(this).apply {
            hint = "PC IP address, e.g. 192.168.1.42"
            setSingleLine(true)
            inputType = InputType.TYPE_CLASS_TEXT
        }

        pcPortEdit = EditText(this).apply {
            hint = "PC UDP port"
            setSingleLine(true)
            inputType = InputType.TYPE_CLASS_NUMBER
            setText("7777")
        }

        val requestPermissionButton = Button(this).apply {
            text = "1. Request Health Connect Permission"
            setOnClickListener {
                requestHealthPermission()
            }
        }

        val sendTestButton = Button(this).apply {
            text = "2. Send Test Packet To PC"
            setOnClickListener {
                sendTestPacket()
            }
        }

        val startButton = Button(this).apply {
            text = "3. Start Health Streaming"
            setOnClickListener {
                startStreaming()
            }
        }

        val stopButton = Button(this).apply {
            text = "Stop Streaming"
            setOnClickListener {
                stopStreaming()
            }
        }

        statusText = TextView(this).apply {
            text = "Status: idle"
            textSize = 16f
            setPadding(0, 30, 0, 10)
        }

        lastSampleText = TextView(this).apply {
            text = "Last sample: none"
            textSize = 16f
        }

        root.addView(title)
        root.addView(explanation)
        root.addView(pcIpEdit)
        root.addView(pcPortEdit)
        root.addView(requestPermissionButton)
        root.addView(sendTestButton)
        root.addView(startButton)
        root.addView(stopButton)
        root.addView(statusText)
        root.addView(lastSampleText)

        val scrollView = ScrollView(this)
        scrollView.addView(root)
        setContentView(scrollView)
    }

    private fun requestHealthPermission() {
        appScope.launch {
            val client = getHealthConnectClientOrNull() ?: return@launch

            val granted = client.permissionController.getGrantedPermissions()
            if (granted.containsAll(permissions)) {
                setStatus("Health Connect permission already granted.")
            } else {
                setStatus("Opening Health Connect permission screen...")
                requestPermissions.launch(permissions)
            }
        }
    }

    private fun sendTestPacket() {
        val target = readPcTargetOrNull() ?: return

        appScope.launch {
            try {
                val measuredAt = Instant.now()
                val json = buildHeartRateJson(
                    bpm = 75,
                    measuredAt = measuredAt,
                    source = "android_test"
                )

                sendUdp(target.ip, target.port, json)

                setStatus("Sent test packet to ${target.ip}:${target.port}")
                setLastSample("Test packet: 75 bpm")
            } catch (e: Exception) {
                setStatus("Test send failed: ${e.message}")
            }
        }
    }

    private fun startStreaming() {
        val target = readPcTargetOrNull() ?: return

        streamingJob?.cancel()
        sentSampleKeys.clear()

        streamingJob = appScope.launch {
            val client = getHealthConnectClientOrNull() ?: return@launch

            val granted = client.permissionController.getGrantedPermissions()
            if (!granted.containsAll(permissions)) {
                setStatus("Permission missing. Tap 'Request Health Connect Permission' first.")
                requestPermissions.launch(permissions)
                return@launch
            }

            val sessionLookbackStart = Instant.now().minus(Duration.ofMinutes(2))

            setStatus("Streaming started. Polling Health Connect every 3 seconds.")

            while (isActive) {
                try {
                    val sentCount = pollHealthConnectAndSend(
                        client = client,
                        rangeStart = sessionLookbackStart,
                        target = target
                    )

                    if (sentCount == 0) {
                        setStatus("Streaming. No new heart-rate samples found in this poll.")
                    } else {
                        setStatus("Streaming. Sent $sentCount new heart-rate sample(s).")
                    }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    setStatus("Streaming error: ${e.message}")
                }

                delay(3000)
            }
        }
    }

    private fun stopStreaming() {
        streamingJob?.cancel()
        streamingJob = null
        setStatus("Streaming stopped.")
    }

    private suspend fun pollHealthConnectAndSend(
        client: HealthConnectClient,
        rangeStart: Instant,
        target: PcTarget
    ): Int = withContext(Dispatchers.IO) {
        val now = Instant.now()
        val samples = mutableListOf<HeartRateRecord.Sample>()

        var pageToken: String? = null

        do {
            val response = client.readRecords(
                ReadRecordsRequest(
                    recordType = HeartRateRecord::class,
                    timeRangeFilter = TimeRangeFilter.between(rangeStart, now),
                    pageToken = pageToken
                )
            )

            for (record in response.records) {
                samples.addAll(record.samples)
            }

            pageToken = response.pageToken
        } while (pageToken != null)

        var sentCount = 0

        for (sample in samples.sortedBy { it.time }) {
            val key = "${sample.time}|${sample.beatsPerMinute}"

            if (!rememberIfNewSample(key)) {
                continue
            }

            val json = buildHeartRateJson(
                bpm = sample.beatsPerMinute,
                measuredAt = sample.time,
                source = "health_connect"
            )

            sendUdp(target.ip, target.port, json)
            sentCount++

            val ageMs = Duration.between(sample.time, Instant.now()).toMillis()

            withContext(Dispatchers.Main) {
                setLastSample(
                    "HR ${sample.beatsPerMinute} bpm | measured ${sample.time} | age ${ageMs / 1000.0}s"
                )
            }
        }

        sentCount
    }

    @Synchronized
    private fun rememberIfNewSample(key: String): Boolean {
        if (sentSampleKeys.contains(key)) {
            return false
        }

        sentSampleKeys.add(key)

        if (sentSampleKeys.size > 10000) {
            val first = sentSampleKeys.iterator().next()
            sentSampleKeys.remove(first)
        }

        return true
    }

    private fun buildHeartRateJson(
        bpm: Long,
        measuredAt: Instant,
        source: String
    ): String {
        val sentAt = Instant.now()
        val ageMs = Duration.between(measuredAt, sentAt).toMillis()

        return JSONObject()
            .put("type", "hr")
            .put("source", source)
            .put("device", "galaxy_fit3_via_samsung_health_health_connect")
            .put("mode", "live")
            .put("seq", seq.incrementAndGet())
            .put("sessionId", "live-health-connect")
            .put("bpm", bpm)
            .put("hr", bpm)
            .put("measuredAt", measuredAt.toString())
            .put("sentAt", sentAt.toString())
            .put("ageMs", ageMs)
            .toString()
    }

    private suspend fun sendUdp(
        ip: String,
        port: Int,
        json: String
    ) = withContext(Dispatchers.IO) {
        val bytes = json.toByteArray(Charsets.UTF_8)
        val address = InetAddress.getByName(ip)

        DatagramSocket().use { socket ->
            val packet = DatagramPacket(bytes, bytes.size, address, port)
            socket.send(packet)
        }
    }

    private fun getHealthConnectClientOrNull(): HealthConnectClient? {
        return when (HealthConnectClient.getSdkStatus(this)) {
            HealthConnectClient.SDK_AVAILABLE -> {
                HealthConnectClient.getOrCreate(this)
            }

            HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> {
                setStatus("Health Connect must be installed or updated on this phone.")
                null
            }

            else -> {
                setStatus("Health Connect is unavailable on this device.")
                null
            }
        }
    }

    private fun readPcTargetOrNull(): PcTarget? {
        val ip = pcIpEdit.text.toString().trim()
        val portText = pcPortEdit.text.toString().trim()

        if (ip.isBlank()) {
            Toast.makeText(this, "Enter the PC IP address.", Toast.LENGTH_SHORT).show()
            return null
        }

        val port = portText.toIntOrNull()
        if (port == null || port !in 1..65535) {
            Toast.makeText(this, "Enter a valid UDP port.", Toast.LENGTH_SHORT).show()
            return null
        }

        return PcTarget(ip = ip, port = port)
    }

    private fun setStatus(message: String) {
        runOnUiThread {
            if (::statusText.isInitialized) {
                statusText.text = "Status: $message"
            }
        }
    }

    private fun setLastSample(message: String) {
        runOnUiThread {
            if (::lastSampleText.isInitialized) {
                lastSampleText.text = "Last sample: $message"
            }
        }
    }

    override fun onDestroy() {
        streamingJob?.cancel()
        appScope.cancel()
        super.onDestroy()
    }

    private data class PcTarget(
        val ip: String,
        val port: Int
    )
}