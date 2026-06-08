# =============================================================
# MAUI / MONO / JNI BRIDGE
# =============================================================
-keep class mono.** { *; }
-keep class java.interop.** { *; }
-keep class crc6*.** { *; }
-keep class md5*.** { *; }
-dontwarn mono.**
-dontwarn java.interop.**
-dontwarn crc6**

# =============================================================
# ANDROIDX LIFECYCLE - EVERY VARIANT
# =============================================================
-keep class androidx.lifecycle.** { *; }
-keep interface androidx.lifecycle.** { *; }
-keep class androidx.lifecycle.DefaultLifecycleObserver { *; }
-keep class androidx.lifecycle.LifecycleObserver { *; }
-keep class androidx.lifecycle.ProcessLifecycleOwner { *; }
-keep class androidx.lifecycle.ProcessLifecycleOwnerInitializer { *; }
-keep class androidx.lifecycle.ReportFragment { *; }
-keep class androidx.lifecycle.LifecycleRegistry { *; }
-keep class androidx.lifecycle.ViewModel { *; }
-keep class androidx.lifecycle.ViewModelStore { *; }
-keep class androidx.lifecycle.ViewModelProvider { *; }
-keep class androidx.lifecycle.LiveData { *; }
-keep class androidx.lifecycle.MutableLiveData { *; }
-keep class androidx.lifecycle.Observer { *; }
-keepclassmembers class * implements androidx.lifecycle.LifecycleObserver {
    <methods>;
}
-keepclassmembers class * extends androidx.lifecycle.ViewModel {
    <init>();
}
-keepclassmembers class androidx.lifecycle.Lifecycle$State { *; }
-keepclassmembers class androidx.lifecycle.Lifecycle$Event { *; }
-dontwarn androidx.lifecycle.**

# =============================================================
# ANDROIDX FRAGMENT / APPCOMPAT / ACTIVITY / CORE
# =============================================================
-keep class androidx.fragment.** { *; }
-keep class androidx.appcompat.** { *; }
-keep class androidx.activity.** { *; }
-keep class androidx.core.** { *; }
-keep class androidx.savedstate.** { *; }
-keep class androidx.startup.** { *; }
-keep class androidx.collection.** { *; }
-keep class androidx.annotation.** { *; }
-keep class androidx.arch.core.** { *; }
-keep class androidx.vectordrawable.** { *; }
-keep class androidx.coordinatorlayout.** { *; }
-keep class androidx.drawerlayout.** { *; }
-keep class androidx.recyclerview.** { *; }
-keep class androidx.viewpager.** { *; }
-keep class androidx.viewpager2.** { *; }
-dontwarn androidx.fragment.**
-dontwarn androidx.appcompat.**
-dontwarn androidx.activity.**
-dontwarn androidx.core.**

# =============================================================
# GOOGLE ML KIT - CRITICAL
# =============================================================
-keep class com.google.mlkit.** { *; }
-keep class com.google.mlkit.common.** { *; }
-keep class com.google.mlkit.common.internal.** { *; }
-keep class com.google.mlkit.common.internal.MlKitInitProvider { *; }
-keep class com.google.mlkit.vision.** { *; }
-keep class com.google.mlkit.vision.barcode.** { *; }
-keep class com.google.mlkit.vision.text.** { *; }
-keep class com.google.mlkit.nl.** { *; }
-dontwarn com.google.mlkit.**

# =============================================================
# GOOGLE PLAY SERVICES / GMS
# =============================================================
-keep class com.google.android.gms.** { *; }
-keep class com.google.android.gms.common.** { *; }
-keep class com.google.android.gms.tasks.** { *; }
-keep class com.google.android.gms.vision.** { *; }
-keep class com.google.android.gms.internal.** { *; }
-dontwarn com.google.android.gms.**

# =============================================================
# KOTLIN
# =============================================================
-keep class kotlin.** { *; }
-keep class kotlin.Metadata { *; }
-keep class kotlinx.** { *; }
-keep class kotlinx.coroutines.** { *; }
-keep class kotlin.jvm.** { *; }
-dontwarn kotlin.**
-dontwarn kotlinx.**
-keepclassmembers class **$WhenMappings {
    <fields>;
}
-keepclassmembers class kotlin.Metadata {
    public <methods>;
}

# =============================================================
# SUPABASE / KTOR / SERIALIZATION
# =============================================================
-keep class io.github.jan.supabase.** { *; }
-keep class io.ktor.** { *; }
-keep class io.ktor.client.** { *; }
-keep class io.ktor.utils.** { *; }
-keep class kotlinx.serialization.** { *; }
-dontwarn io.ktor.**
-dontwarn io.github.jan.supabase.**
-dontwarn kotlinx.serialization.**

# =============================================================
# SIGNALR
# =============================================================
-keep class com.microsoft.signalr.** { *; }
-keep class microsoft.aspnetcore.** { *; }
-dontwarn com.microsoft.signalr.**

# =============================================================
# OKHTTP / OKIO (used by Ktor and SignalR)
# =============================================================
-keep class okhttp3.** { *; }
-keep class okio.** { *; }
-dontwarn okhttp3.**
-dontwarn okio.**
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**

# =============================================================
# NEWTONSOFT JSON
# =============================================================
-keep class com.newtonsoft.** { *; }
-keepattributes *Annotation*
-keepattributes Signature
-keepattributes Exceptions
-keepattributes InnerClasses
-keepattributes EnclosingMethod
-keepattributes LineNumberTable
-keepattributes SourceFile

# =============================================================
# ANDROID COMPONENTS - KEEP ALL ENTRY POINTS
# =============================================================
-keep public class * extends android.app.Activity
-keep public class * extends android.app.Application
-keep public class * extends android.app.Service
-keep public class * extends android.content.BroadcastReceiver
-keep public class * extends android.content.ContentProvider
-keep public class * extends android.app.backup.BackupAgentHelper
-keep public class * extends android.preference.Preference
-keep public class * extends android.view.View {
    public <init>(android.content.Context);
    public <init>(android.content.Context, android.util.AttributeSet);
    public <init>(android.content.Context, android.util.AttributeSet, int);
}

# =============================================================
# PARCELABLE / SERIALIZABLE
# =============================================================
-keep class * implements java.io.Serializable {
    private static final java.io.ObjectStreamField[] serialPersistentFields;
    private void writeObject(java.io.ObjectOutputStream);
    private void readObject(java.io.ObjectInputStream);
    java.lang.Object writeReplace();
    java.lang.Object readResolve();
}
-keep class * implements android.os.Parcelable {
    public static final android.os.Parcelable$Creator *;
}
-keepclassmembers class * implements android.os.Parcelable {
    static ** CREATOR;
}

# =============================================================
# REFLECTION SAFETY
# =============================================================
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}
-keepclasseswithmembernames class * {
    native <methods>;
}
-keepclasseswithmembers class * {
    public <init>(android.content.Context, android.util.AttributeSet);
}
-keepclasseswithmembers class * {
    public <init>(android.content.Context, android.util.AttributeSet, int);
}
-keepclassmembers enum * {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}

# =============================================================
# SUPPRESS COMMON WARNINGS
# =============================================================
-dontwarn sun.misc.**
-dontwarn java.lang.invoke.**
-dontwarn javax.annotation.**
-dontwarn org.slf4j.**
-dontwarn org.apache.**
-dontwarn com.sun.**
-dontwarn dalvik.**
-dontwarn org.xmlpull.**