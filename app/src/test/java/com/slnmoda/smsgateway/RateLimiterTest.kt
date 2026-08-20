package com.slnmoda.smsgateway

import com.slnmoda.smsgateway.server.RateLimiter
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RateLimiterTest {

    @Test
    fun `ayni numaraya pencere icinde ikinci istek reddedilir`() {
        val limiter = RateLimiter(windowMs = 60_000)
        val phone = "+905321112233"
        assertTrue(limiter.tryAcquire(phone, nowMs = 0))
        assertFalse(limiter.tryAcquire(phone, nowMs = 30_000))
    }

    @Test
    fun `pencere gectikten sonra tekrar izin verilir`() {
        val limiter = RateLimiter(windowMs = 60_000)
        val phone = "+905321112233"
        assertTrue(limiter.tryAcquire(phone, nowMs = 0))
        assertTrue(limiter.tryAcquire(phone, nowMs = 60_001))
    }

    @Test
    fun `farkli numaralar birbirini etkilemez`() {
        val limiter = RateLimiter(windowMs = 60_000)
        assertTrue(limiter.tryAcquire("+905321112233", nowMs = 0))
        assertTrue(limiter.tryAcquire("+905324445566", nowMs = 0))
    }
}
