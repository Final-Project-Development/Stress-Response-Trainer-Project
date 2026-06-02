package com.fit3bridge.samsung

import android.util.Log
import android.app.Activity
import android.os.Bundle
import android.text.InputType
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import com.samsung.android.sdk.health.data.HealthDataService
import com.samsung.android.sdk.health.data.HealthDataStore
import com.samsung.android.sdk.health.data.data.HealthDataPoint
import com.samsung.android.sdk.health.data.data.entries.HeartRate
import com.samsung.android.sdk.health.data.error.ResolvablePlatformException
import com.samsung.android.sdk.health.data.permission.AccessType
import com.samsung.android.sdk.health.data.permission.Permission
import com.samsung.android.sdk.health.data.request.DataType
import com.samsung.android.sdk.health.data.request.DataTypes
import com.samsung.android.sdk.health.data.request.LocalTimeFilter
import com.samsung.android.sdk.health.data.request.Ordering
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
import java.time.LocalDateTime
import java.util.LinkedHashSet
import java.util.concurrent.atomic.AtomicLong
import java.io.BufferedWriter
import java.io.OutputStreamWriter
import java.net.InetSocketAddress
import java.net.Socket
import java.time.ZoneId

class MainActivity : Activity() {

    private lateinit var pcIpEdit: EditText
    private lateinit var pcPortEdit: EditText
    private lateinit var statusText: TextView
    private lateinit var lastSampleText: TextView

    private val appScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var streamingJob: Job? = null

    private val seq = AtomicLong(0)
    private val sentSampleKeys = LinkedHashSet<String>()

    private val requiredPermissions = hashSetOf(
        Permission.of(DataTypes.HEART_RATE, AccessType.READ)
    )

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
            text = "Fit3 Samsung Bridge"
            textSize = 24f
        }

        val explanation = TextView(this).apply {
            text = """
                This app reads heart-rate data directly from Samsung Health using Samsung Health Data SDK.

                Start the PC bridge first.
                Then enter the PC IPv4 address.
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
            text = "1. Request Samsung Health Permission"
            setOnClickListener {
                requestSamsungHealthPermission()
            }
        }

        val sendTestButton = Button(this).apply {
            text = "2. Send Test Packet To PC"
            setOnClickListener {
                sendTestPacket()
            }
        }

        val startButton = Button(this).apply {
            text = "3. Start Samsung SDK Streaming"
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

    private fun requestSamsungHealthPermission() {
        appScope.launch {
            val store = getSamsungHealthStoreOrNull() ?: return@launch
            val ok = ensureSamsungHealthPermission(store)

            if (ok) {
                setStatus("Samsung Health heart-rate permission granted.")
            } else {
                setStatus("Samsung Health heart-rate permission was not granted.")
            }
        }
    }

    private fun sendTestPacket() {
        val target = readPcTargetOrNull() ?: return

        appScope.launch {
            try {
                val measuredAt = Instant.now()

                val json = buildHeartRateJson(
                    bpm = 75f,
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

        // This marker defines "from now".
        // We subtract 2 minutes so we do not miss samples if the workout started just before pressing the button.
        val watcherStartedAt = Instant.now().minus(Duration.ofMinutes(2))

        streamingJob = appScope.launch {
            try {
                setStatus("Connecting to Samsung Health...")

                val store = getSamsungHealthStoreOrNull()
                if (store == null) {
                    setStatus("Could not connect to Samsung Health.")
                    return@launch
                }

                setStatus("Checking Samsung Health permission...")

                val ok = ensureSamsungHealthPermission(store)
                if (!ok) {
                    setStatus("Samsung Health permission missing or blocked.")
                    return@launch
                }

                setStatus("Watching for saved Fit3 workout HR timeline.")

                while (isActive) {
                    try {
                        val sentCount = pollSamsungHealthAndSend(
                            store = store,
                            target = target,
                            watcherStartedAt = watcherStartedAt
                        )

                        if (sentCount == 0) {
                            setStatus("Watching. No new saved HR timeline yet.")
                        } else {
                            setStatus("Sent full HR timeline with $sentCount sample(s).")
                        }
                    } catch (t: Throwable) {
                        Log.e("Fit3SamsungBridge", "Post-workout polling failed", t)

                        setStatus("Polling failed: ${throwableToShortString(t)}")
                        setLastSample("Open Logcat and search Fit3SamsungBridge.")

                        delay(3000)
                    }

                    delay(3000)
                }
            } catch (t: Throwable) {
                Log.e("Fit3SamsungBridge", "Timeline watcher crashed", t)

                setStatus("Timeline watcher crashed: ${throwableToShortString(t)}")
                setLastSample("Open Logcat and copy the FATAL EXCEPTION block.")
            }
        }
    }

    private fun stopStreaming() {
        streamingJob?.cancel()
        streamingJob = null
        setStatus("Streaming stopped.")
    }

    private suspend fun getSamsungHealthStoreOrNull(): HealthDataStore? {
        return withContext(Dispatchers.IO) {
            try {
                HealthDataService.getStore(applicationContext)
            } catch (e: ResolvablePlatformException) {
                withContext(Dispatchers.Main) {
                    setStatus("Samsung Health needs setup. Opening resolver if available.")

                    if (e.hasResolution) {
                        e.resolve(this@MainActivity)
                    }
                }
                null
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    setStatus("Cannot connect to Samsung Health: ${e.message}")
                }
                null
            }
        }
    }

    private suspend fun ensureSamsungHealthPermission(store: HealthDataStore): Boolean {
        return try {
            val granted = withContext(Dispatchers.IO) {
                store.getGrantedPermissions(requiredPermissions)
            }

            if (granted.containsAll(requiredPermissions)) {
                true
            } else {
                val requestResult = withContext(Dispatchers.Main) {
                    store.requestPermissions(requiredPermissions, this@MainActivity)
                }

                requestResult.containsAll(requiredPermissions)
            }
        } catch (e: ResolvablePlatformException) {
            if (e.hasResolution) {
                e.resolve(this@MainActivity)
            }

            setStatus("Samsung Health platform setup required.")
            false
        } catch (e: Exception) {
            setStatus("Permission error: ${e.message}")
            false
        }
    }

    private fun throwableToShortString(t: Throwable): String{
        val name = t::class.java.simpleName
        val message = t.message ?: "no message"
        return "$name: $message"
    }

    private suspend fun pollSamsungHealthAndSend(
        store: HealthDataStore,
        target: PcTarget,
        watcherStartedAt: Instant
    ): Int = withContext(Dispatchers.IO) {

        val nowInstant = Instant.now()

        val startTime = LocalDateTime.ofInstant(
            watcherStartedAt,
            ZoneId.systemDefault()
        )

        val endTime = LocalDateTime.now()

        val readRequest = DataTypes.HEART_RATE.readDataRequestBuilder
            .setLocalTimeFilter(LocalTimeFilter.of(startTime, endTime))
            .setOrdering(Ordering.ASC)
            .build()

        val response = store.readData(readRequest)
        val dataPoints = response.dataList

        Log.d(
            "Fit3SamsungBridge",
            "Timeline query returned ${dataPoints.size} HR point(s)."
        )

        data class HrCandidate(
            val bpm: Float,
            val measuredAt: Instant,
            val source: String
        )

        val candidates = mutableListOf<HrCandidate>()

        for (point in dataPoints) {

            // 1. Try normal point-level HR value.
            val pointBpm: Float? = try {
                point.getValue(DataType.HeartRateType.HEART_RATE)
            } catch (t: Throwable) {
                null
            }

            if (pointBpm != null && pointBpm > 0f) {
                candidates.add(
                    HrCandidate(
                        bpm = pointBpm,
                        measuredAt = point.startTime,
                        source = "samsung_health_sdk_point"
                    )
                )
            }

            // 2. Try continuous series HR values.
            val series: List<HeartRate>? = try {
                point.getValue(DataType.HeartRateType.SERIES_DATA)
            } catch (t: Throwable) {
                null
            }

            if (!series.isNullOrEmpty()) {
                for (sample in series) {
                    val bpm = sample.heartRate

                    if (bpm > 0f) {
                        candidates.add(
                            HrCandidate(
                                bpm = bpm,
                                measuredAt = sample.startTime,
                                source = "samsung_health_sdk_series"
                            )
                        )
                    }
                }
            }
        }

        val cleanSamples = candidates
            .filter { sample ->
                !sample.measuredAt.isBefore(watcherStartedAt) &&
                        !sample.measuredAt.isAfter(nowInstant.plusSeconds(60))
            }
            .distinctBy { sample ->
                "${sample.measuredAt.toEpochMilli()}|${sample.bpm}"
            }
            .sortedBy { sample ->
                sample.measuredAt
            }

        if (cleanSamples.isEmpty()) {
            withContext(Dispatchers.Main) {
                setLastSample(
                    "No HR samples saved since watcher started. Samsung returned ${dataPoints.size} HR point(s)."
                )
            }

            return@withContext 0
        }

        // Split samples into sessions. A gap larger than 10 minutes means a different workout/session.
        val sessions = mutableListOf<MutableList<HrCandidate>>()
        var currentSession = mutableListOf<HrCandidate>()

        for (sample in cleanSamples) {
            if (currentSession.isEmpty()) {
                currentSession.add(sample)
                continue
            }

            val previousTime = currentSession.last().measuredAt
            val gap = Duration.between(previousTime, sample.measuredAt)

            if (gap <= Duration.ofMinutes(10)) {
                currentSession.add(sample)
            } else {
                sessions.add(currentSession)
                currentSession = mutableListOf(sample)
            }
        }

        if (currentSession.isNotEmpty()) {
            sessions.add(currentSession)
        }

        val latestSession = sessions.maxByOrNull { session ->
            session.last().measuredAt
        }

        if (latestSession == null || latestSession.isEmpty()) {
            withContext(Dispatchers.Main) {
                setLastSample("No valid HR session found.")
            }

            return@withContext 0
        }

        val sessionStart = latestSession.first().measuredAt
        val sessionEnd = latestSession.last().measuredAt
        val sampleCount = latestSession.size

        val sessionId =
            "fit3-${sessionStart.toEpochMilli()}-${sessionEnd.toEpochMilli()}-$sampleCount"

        val sessionKey = "sent_session|$sessionId"

        if (sentSampleKeys.contains(sessionKey)) {
            val newestAgeMs = Duration.between(sessionEnd, nowInstant).toMillis()

            withContext(Dispatchers.Main) {
                setLastSample(
                    "Latest timeline already sent. samples=$sampleCount, start=$sessionStart, end=$sessionEnd, newestAge=${newestAgeMs / 1000.0}s"
                )
            }

            return@withContext 0
        }

        val messages = mutableListOf<String>()

        messages.add(
            buildSessionMarkerJson(
                type = "hr_session_start",
                sessionId = sessionId,
                startedAt = sessionStart,
                endedAt = sessionEnd,
                sampleCount = sampleCount
            )
        )

        for ((index, sample) in latestSession.withIndex()) {
            messages.add(
                buildTimelineSampleJson(
                    sessionId = sessionId,
                    sampleIndex = index,
                    sampleCount = sampleCount,
                    bpm = sample.bpm,
                    measuredAt = sample.measuredAt,
                    source = sample.source
                )
            )
        }

        messages.add(
            buildSessionMarkerJson(
                type = "hr_session_end",
                sessionId = sessionId,
                startedAt = sessionStart,
                endedAt = sessionEnd,
                sampleCount = sampleCount
            )
        )

        sendTcpJsonLines(
            ip = target.ip,
            port = target.port,
            messages = messages
        )

        rememberIfNewSample(sessionKey)

        val newestAgeMs = Duration.between(sessionEnd, Instant.now()).toMillis()

        withContext(Dispatchers.Main) {
            setLastSample(
                "SENT FULL TIMELINE: $sampleCount samples | start=$sessionStart | end=$sessionEnd | newestAge=${newestAgeMs / 1000.0}s"
            )
        }

        sampleCount
    }

    @Synchronized
    private fun rememberIfNewSample(key: String): Boolean {
        if (sentSampleKeys.contains(key)) {
            return false
        }

        sentSampleKeys.add(key)

        if (sentSampleKeys.size > 20000) {
            val first = sentSampleKeys.iterator().next()
            sentSampleKeys.remove(first)
        }

        return true
    }

    private fun buildHeartRateJson(
        bpm: Float,
        measuredAt: Instant,
        source: String
    ): String {
        val sentAt = Instant.now()
        val ageMs = Duration.between(measuredAt, sentAt).toMillis()

        return JSONObject()
            .put("type", "hr")
            .put("source", source)
            .put("device", "galaxy_fit3_via_samsung_health_data_sdk")
            .put("seq", seq.incrementAndGet())
            .put("bpm", bpm)
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

    private fun buildSessionMarkerJson(
        type: String,
        sessionId: String,
        startedAt: Instant,
        endedAt: Instant,
        sampleCount: Int
    ): String {
        val sentAt = Instant.now()

        return JSONObject()
            .put("type", type)
            .put("source", "samsung_health_sdk_post_workout")
            .put("device", "galaxy_fit3_via_samsung_health_data_sdk")
            .put("seq", seq.incrementAndGet())
            .put("sessionId", sessionId)
            .put("startedAt", startedAt.toString())
            .put("endedAt", endedAt.toString())
            .put("sampleCount", sampleCount)
            .put("sentAt", sentAt.toString())
            .toString()
    }

    private fun buildTimelineSampleJson(
        sessionId: String,
        sampleIndex: Int,
        sampleCount: Int,
        bpm: Float,
        measuredAt: Instant,
        source: String
    ): String {
        val sentAt = Instant.now()
        val ageMs = Duration.between(measuredAt, sentAt).toMillis()

        return JSONObject()
            .put("type", "hr")
            .put("mode", "post_workout_timeline")
            .put("source", source)
            .put("device", "galaxy_fit3_via_samsung_health_data_sdk")
            .put("seq", seq.incrementAndGet())
            .put("sessionId", sessionId)
            .put("sampleIndex", sampleIndex)
            .put("sampleCount", sampleCount)
            .put("bpm", bpm)
            .put("measuredAt", measuredAt.toString())
            .put("sentAt", sentAt.toString())
            .put("ageMs", ageMs)
            .toString()
    }

    private suspend fun sendTcpJsonLines(
        ip: String,
        port: Int,
        messages: List<String>
    ) = withContext(Dispatchers.IO) {
        Socket().use { socket ->
            socket.connect(
                InetSocketAddress(ip, port),
                5000
            )

            socket.soTimeout = 10000

            BufferedWriter(
                OutputStreamWriter(
                    socket.getOutputStream(),
                    Charsets.UTF_8
                )
            ).use { writer ->
                for (message in messages) {
                    writer.write(message)
                    writer.newLine()
                }

                writer.flush()
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