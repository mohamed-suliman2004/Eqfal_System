import java.util.Properties
import java.io.FileInputStream

plugins {
    id("com.android.application")
    id("dev.flutter.flutter-gradle-plugin")
}

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}

android {
    namespace = "ly.mostanad.eqfal_app"
    compileSdk = 36
    ndkVersion = "28.2.13676358"

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    signingConfigs {
        create("release") {
            keyAlias = keystoreProperties.getProperty("keyAlias") ?: "upload"
            keyPassword = keystoreProperties.getProperty("keyPassword") ?: "EqfalGooglePlayKey2026!"
            storeFile = keystoreProperties.getProperty("storeFile")?.let { file(it) } ?: file("upload-keystore.jks")
            storePassword = keystoreProperties.getProperty("storePassword") ?: "EqfalGooglePlayKey2026!"
        }
    }

    defaultConfig {
        applicationId = "ly.mostanad.eqfal_app"
        minSdk = flutter.minSdkVersion
        targetSdk = 36
        versionCode = 11
        versionName = "1.0.11"
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.getByName("release")
            isMinifyEnabled = false
            isShrinkResources = false
            ndk {
                debugSymbolLevel = "NONE"
            }
            packaging {
                jniLibs {
                    useLegacyPackaging = true
                }
            }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
