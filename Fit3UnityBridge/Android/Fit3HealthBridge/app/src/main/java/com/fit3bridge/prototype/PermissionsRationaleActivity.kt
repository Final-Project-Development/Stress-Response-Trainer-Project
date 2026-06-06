package com.fit3bridge.prototype

import android.app.Activity
import android.os.Bundle
import android.widget.ScrollView
import android.widget.TextView

class PermissionsRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val textView = TextView(this).apply {
            textSize = 18f
            setPadding(40, 40, 40, 40)
            text = """
                Fit3 Health Bridge privacy rationale

                This prototype reads heart-rate data from Health Connect after you grant permission.

                Data used:
                - Heart-rate samples
                - Measurement timestamp
                - Beats per minute

                Data destination:
                - The local Windows PC IP address you enter in the app
                - UDP port 7777 on the same local Wi-Fi network

                This prototype does not diagnose, treat, or monitor a medical condition.
                It is used only for local VR biofeedback prototype testing.
            """.trimIndent()
        }

        val scrollView = ScrollView(this)
        scrollView.addView(textView)

        setContentView(scrollView)
    }
}