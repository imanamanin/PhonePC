package com.phonecontrol.agent.ui

import android.animation.Animator
import android.animation.AnimatorListenerAdapter
import android.animation.AnimatorSet
import android.animation.ValueAnimator
import android.content.Context
import android.graphics.Canvas
import android.graphics.LinearGradient
import android.graphics.Paint
import android.graphics.RectF
import android.graphics.Shader
import android.util.AttributeSet
import android.view.View
import android.view.animation.AccelerateDecelerateInterpolator
import android.view.animation.DecelerateInterpolator
import com.phonecontrol.agent.R
import kotlin.math.min
import kotlin.random.Random

/**
 * Presentation-only splash: morphs a phone silhouette into a desktop.
 * Does not start services or change connection behavior.
 */
class SplashMorphView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    var onFinished: (() -> Unit)? = null

    private var morph = 0f
    private var brand = 0f
    private val body = RectF()
    private val screen = RectF()
    private val island = RectF()
    private val stand = RectF()
    private val particles = Array(16) { Particle() }

    private val navy = 0xFF06101C.toInt()
    private val turquoise = 0xFF30B4D3.toInt()
    private val orange = 0xFFF77F08.toInt()
    private val sand = 0xFFE9D8B6.toInt()

    private val bgPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val fillPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val strokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 3f
        color = turquoise
    }
    private val glowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 8f
        color = 0x5530B4D3
    }
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = 0xFFEAF3FF.toInt()
        textAlign = Paint.Align.CENTER
        letterSpacing = 0.04f
    }
    private val subPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = sand
        textAlign = Paint.Align.CENTER
    }
    private val particlePaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val patternPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 1.2f
        color = 0x2230B4D3
    }

    private val title = context.getString(R.string.brand_name)
    private val subtitle = context.getString(R.string.brand_studio)

    private var animator: AnimatorSet? = null

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        if (visibility == VISIBLE) {
            start()
        }
    }

    override fun onDetachedFromWindow() {
        animator?.cancel()
        super.onDetachedFromWindow()
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        bgPaint.shader = LinearGradient(
            0f, 0f, w.toFloat(), h.toFloat(),
            navy, 0xFF0A1C33.toInt(),
            Shader.TileMode.CLAMP
        )
        val cx = w / 2f
        val cy = h / 2.15f
        particles.forEachIndexed { index, particle ->
            val angle = (index / particles.size.toFloat()) * Math.PI * 2
            particle.ox = cx
            particle.oy = cy
            particle.dx = kotlin.math.cos(angle).toFloat() * (80f + Random.nextFloat() * 90f)
            particle.dy = kotlin.math.sin(angle).toFloat() * (70f + Random.nextFloat() * 80f)
            particle.size = 2.2f + Random.nextFloat() * 3.4f
            particle.orange = index % 3 == 0
        }
    }

    fun start() {
        animator?.cancel()
        morph = 0f
        brand = 0f
        SoftChimes.splash()
        val morphAnim = ValueAnimator.ofFloat(0f, 1f).apply {
            duration = 1800
            interpolator = AccelerateDecelerateInterpolator()
            addUpdateListener {
                morph = it.animatedValue as Float
                invalidate()
            }
        }
        val brandAnim = ValueAnimator.ofFloat(0f, 1f).apply {
            duration = 700
            interpolator = DecelerateInterpolator()
            addUpdateListener {
                brand = it.animatedValue as Float
                invalidate()
            }
        }
        val holdAnim = ValueAnimator.ofFloat(1f, 1f).apply {
            duration = 1600
        }
        animator = AnimatorSet().apply {
            playSequentially(morphAnim, brandAnim, holdAnim)
            addListener(object : AnimatorListenerAdapter() {
                override fun onAnimationEnd(animation: Animator) {
                    postDelayed({ onFinished?.invoke() }, 200)
                }
            })
            start()
        }
    }

    override fun onDraw(canvas: Canvas) {
        val w = width.toFloat()
        val h = height.toFloat()
        canvas.drawRect(0f, 0f, w, h, bgPaint)
        drawPattern(canvas, w, h)

        val min = min(w, h)
        val cx = w / 2f
        val cy = h / 2.2f
        val t = morph
        val phoneW = min * 0.26f
        val phoneH = min * 0.52f
        val pcW = min * 0.58f
        val pcH = min * 0.32f
        val bodyW = lerp(phoneW, pcW, t)
        val bodyH = lerp(phoneH, pcH, t)
        val radius = lerp(bodyW * 0.18f, dp(14f), t)
        body.set(cx - bodyW / 2f, cy - bodyH / 2f, cx + bodyW / 2f, cy + bodyH / 2f)

        glowPaint.alpha = (70 + 80 * t).toInt().coerceAtMost(160)
        glowPaint.strokeWidth = dp(10f) * (0.6f + t)
        canvas.drawRoundRect(body, radius + dp(8f), radius + dp(8f), glowPaint)

        fillPaint.color = 0xFF12233A.toInt()
        canvas.drawRoundRect(body, radius, radius, fillPaint)
        strokePaint.alpha = (180 + 75 * t).toInt()
        canvas.drawRoundRect(body, radius, radius, strokePaint)

        val inset = lerp(dp(8f), dp(10f), t)
        screen.set(body.left + inset, body.top + inset, body.right - inset, body.bottom - inset)
        fillPaint.color = 0xFF083C91.toInt()
        canvas.drawRoundRect(screen, radius * 0.55f, radius * 0.55f, fillPaint)

        val islandW = lerp(bodyW * 0.34f, dp(8f), t)
        val islandH = lerp(dp(8f), dp(8f), t)
        island.set(
            cx - islandW / 2f,
            body.top + lerp(dp(10f), dp(8f), t),
            cx + islandW / 2f,
            body.top + lerp(dp(10f), dp(8f), t) + islandH
        )
        fillPaint.color = 0xFF30B4D3.toInt()
        canvas.drawRoundRect(island, islandH / 2f, islandH / 2f, fillPaint)

        if (t > 0.42f) {
            val standT = ((t - 0.42f) / 0.58f).coerceIn(0f, 1f)
            val standW = lerp(dp(18f), min * 0.16f, standT)
            val neckH = dp(18f) * standT
            fillPaint.color = 0xFF30B4D3.toInt()
            fillPaint.alpha = (standT * 255).toInt()
            stand.set(cx - dp(4f), body.bottom, cx + dp(4f), body.bottom + neckH)
            canvas.drawRoundRect(stand, dp(3f), dp(3f), fillPaint)
            stand.set(
                cx - standW / 2f,
                body.bottom + neckH,
                cx + standW / 2f,
                body.bottom + neckH + dp(7f)
            )
            canvas.drawRoundRect(stand, dp(4f), dp(4f), fillPaint)
            fillPaint.alpha = 255
        }

        if (t in 0.18f..0.92f) {
            val lineT = (t - 0.18f) / 0.74f
            strokePaint.strokeWidth = dp(1.4f)
            strokePaint.alpha = (140 * (1f - kotlin.math.abs(lineT - 0.5f) * 2f)).toInt()
            val span = min * 0.42f * lineT
            canvas.drawLine(cx - span, cy, cx + span, cy, strokePaint)
            canvas.drawLine(cx, cy - span * 0.45f, cx, cy + span * 0.45f, strokePaint)
            strokePaint.strokeWidth = 3f
            strokePaint.alpha = 255
        }

        particles.forEach { particle ->
            val p = (t * 1.15f).coerceIn(0f, 1f)
            particlePaint.color = if (particle.orange) orange else turquoise
            particlePaint.alpha = ((1f - p) * 210).toInt()
            canvas.drawCircle(
                particle.ox + particle.dx * p,
                particle.oy + particle.dy * p,
                particle.size * (1.1f - p * 0.4f),
                particlePaint
            )
        }

        if (brand > 0f) {
            textPaint.textSize = min * 0.072f
            textPaint.alpha = (brand * 255).toInt()
            canvas.drawText(title, cx, body.bottom + dp(72f), textPaint)
            subPaint.textSize = min * 0.028f
            subPaint.alpha = (brand * 230).toInt()
            canvas.drawText(subtitle, cx, body.bottom + dp(96f), subPaint)
        }
    }

    private fun drawPattern(canvas: Canvas, w: Float, h: Float) {
        val size = min(w, h) * 0.22f
        val cx = w - size * 0.35f
        val cy = size * 0.42f
        patternPaint.alpha = 40
        canvas.drawCircle(cx, cy, size * 0.42f, patternPaint)
        canvas.drawCircle(cx, cy, size * 0.28f, patternPaint)
        val star = size * 0.38f
        val pts = FloatArray(16)
        for (i in 0 until 8) {
            val a = (i / 8f) * Math.PI * 2 - Math.PI / 2
            pts[i * 2] = cx + kotlin.math.cos(a).toFloat() * star
            pts[i * 2 + 1] = cy + kotlin.math.sin(a).toFloat() * star
        }
        for (i in 0 until 8) {
            val j = (i + 3) % 8
            canvas.drawLine(pts[i * 2], pts[i * 2 + 1], pts[j * 2], pts[j * 2 + 1], patternPaint)
        }
        patternPaint.alpha = 28
        canvas.drawCircle(size * 0.2f, h - size * 0.18f, size * 0.2f, patternPaint)
    }

    private fun lerp(a: Float, b: Float, t: Float) = a + (b - a) * t

    private fun dp(value: Float) = value * resources.displayMetrics.density

    private class Particle {
        var ox = 0f
        var oy = 0f
        var dx = 0f
        var dy = 0f
        var size = 3f
        var orange = false
    }
}
