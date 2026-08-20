plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
}

// Release imzalama bilgileri ortam degiskenlerinden okunur (CI icin).
// Yerelde bu degiskenler yoksa release imzasiz kalir; gizli bilgi repoya girmez.
val keystoreFile: String? = System.getenv("KEYSTORE_FILE")

android {
    namespace = "com.slnmoda.smsgateway"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.slnmoda.smsgateway"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"

        // Yerel HTTP sunucusunun dinleyeceği port. Spec: 8080.
        buildConfigField("int", "GATEWAY_PORT", "8080")

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    signingConfigs {
        if (keystoreFile != null) {
            create("release") {
                storeFile = file(keystoreFile)
                storePassword = System.getenv("KEYSTORE_PASSWORD")
                keyAlias = System.getenv("KEY_ALIAS")
                keyPassword = System.getenv("KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            // Imza bilgisi mevcutsa release'i imzala; degilse imzasiz uretilir.
            if (keystoreFile != null) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }

    buildFeatures {
        buildConfig = true
        viewBinding = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.material)
    implementation(libs.androidx.constraintlayout)

    // Yaşam döngüsü + servis
    implementation(libs.androidx.lifecycle.service)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.kotlinx.coroutines.android)

    // Hafif gömülü HTTP sunucusu
    implementation(libs.nanohttpd)

    // JSON serileştirme
    implementation(libs.kotlinx.serialization.json)

    // SQLite / Room ile log kalıcılığı
    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)

    // API anahtarını şifreli saklamak için (EncryptedSharedPreferences)
    implementation(libs.androidx.security.crypto)

    testImplementation(libs.junit)
    androidTestImplementation(libs.androidx.junit)
}
