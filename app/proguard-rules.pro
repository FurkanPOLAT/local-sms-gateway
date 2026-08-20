# NanoHTTPD yansıma (reflection) kullanmaz, özel kural gerekmez.

# kotlinx.serialization üretilmiş serializer'ları korunmalı.
-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.**
-keepclassmembers class **$$serializer { *; }
-keepclasseswithmembers class com.slnmoda.smsgateway.** {
    kotlinx.serialization.KSerializer serializer(...);
}
-keep,includedescriptorclasses class com.slnmoda.smsgateway.**$$serializer { *; }

# Room entity/DAO'ları
-keep class com.slnmoda.smsgateway.data.** { *; }
