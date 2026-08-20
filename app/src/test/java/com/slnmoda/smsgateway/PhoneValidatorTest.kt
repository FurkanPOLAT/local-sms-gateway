package com.slnmoda.smsgateway

import com.slnmoda.smsgateway.util.PhoneValidator
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PhoneValidatorTest {

    @Test
    fun `gecerli E164 numarasi normalize edilir`() {
        assertEquals("+905321112233", PhoneValidator.normalizeOrNull("+90 532 111 22 33"))
        assertEquals("+905321112233", PhoneValidator.normalizeOrNull("+90-532-111-2233"))
    }

    @Test
    fun `eksik veya hatali numaralar reddedilir`() {
        assertNull(PhoneValidator.normalizeOrNull(null))
        assertNull(PhoneValidator.normalizeOrNull(""))
        assertNull(PhoneValidator.normalizeOrNull("05321112233")) // + yok
        assertNull(PhoneValidator.normalizeOrNull("+0532"))        // cok kisa / gecersiz onek
        assertNull(PhoneValidator.normalizeOrNull("abc"))
    }
}
