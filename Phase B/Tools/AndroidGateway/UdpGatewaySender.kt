package com.braude.stressgateway

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import kotlin.concurrent.thread

/**
 * Minimal Android gateway: sends HR and HRV text packets to Unity over UDP (same LAN as the PC).
 * Replace [sampleHr] / [sampleHrvMs] with values from your Wear OS / Samsung Health / custom watch pipeline.
 *
 * Unity listens on port 5005 by default ([UDPReceiver.listenPort]).
 * Payload format: "HR:72,HRV:48.5" (see [UDPReceiver] in the Unity project).
 */
class UdpGatewaySenderActivity : AppCompatActivity() {

    private var running = false

    /** PC IPv4 on the LAN, e.g. "192.168.1.42" */
    private val unityHost = "192.168.1.42"

    private val unityPort = 5005
    private val sendIntervalMs = 200L

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        running = true
        thread { streamLoop() }
    }

    override fun onDestroy() {
        running = false
        super.onDestroy()
    }

    private fun streamLoop() {
        var socket: DatagramSocket? = null
        try {
            socket = DatagramSocket()
            val address = InetAddress.getByName(unityHost)
            while (running) {
                val sampleHr = 72
                val sampleHrvMs = 52f
                val payload = "HR:$sampleHr,HRV:$sampleHrvMs"
                val bytes = payload.toByteArray(Charsets.UTF_8)
                socket.send(DatagramPacket(bytes, bytes.size, address, unityPort))
                Thread.sleep(sendIntervalMs)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        } finally {
            socket?.close()
        }
    }
}
