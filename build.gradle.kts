// Kök proje build script'i. Alt modüllere plugin'leri yalnızca tanıtır, uygulamaz.
plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.serialization) apply false
    alias(libs.plugins.ksp) apply false
}
