//
//  Login.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//

import Foundation
import CryptoKit
import SwiftUI
import UIKit

class DeviceManager {
    static let shared = DeviceManager()
    private let key = "savedIdentifierForVendor"

    private init() {}

    var identifierForVendor: String {
        if let savedId = UserDefaults.standard.string(forKey: key) {
            return savedId
        } else {
            let newId = UIDevice.current.identifierForVendor?.uuidString ?? UUID().uuidString
            UserDefaults.standard.set(newId, forKey: key)
            return newId
        }
    }
}

let fingerprint = DeviceManager.shared.identifierForVendor
func getFixedNonce() -> Data {
    // Use a fixed nonce or store the same nonce across registration and login
    // For example, you could use a static 12-byte nonce.
    return Data(repeating: 0, count: 12) // Fixed nonce (unsafe for production but useful for testing)
}

func clearKeychainItem(tokenType: String) {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: tokenType
    ]
    let status = SecItemDelete(query as CFDictionary)
    print("Keychain clear status: \(status)")
}

func saveJWTToKeychain(token: String, tokenType: String) -> Bool {
    let tokenData = token.data(using: .utf8)!
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: tokenType,
        kSecValueData as String: tokenData
    ]
    
    let status = SecItemAdd(query as CFDictionary, nil)
    print("Keychain status after adding token: \(status)") // Debugging log
    
    if status == errSecSuccess {
        return true
    } else if status == errSecDuplicateItem {
        let attributesToUpdate: [String: Any] = [kSecValueData as String: tokenData]
        let updateStatus = SecItemUpdate(query as CFDictionary, attributesToUpdate as CFDictionary)
        print("Keychain status after updating token: \(updateStatus)") // Debugging log
        return updateStatus == errSecSuccess
    }
    
    return false
}
func getJWTFromKeychain(tokenType: String) -> String? {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: tokenType,
        kSecReturnData as String: true,
        kSecMatchLimit as String: kSecMatchLimitOne
    ]
    
    var result: AnyObject?
    let status = SecItemCopyMatching(query as CFDictionary, &result)
    
    if status == errSecSuccess, let data = result as? Data {
        guard let token = String(data: data, encoding: .utf8) else {
            print("Failed to decode keychain data for token type: \(tokenType).")
            return nil
        }
        print("Successfully retrieved token for \(tokenType) from Keychain.")
        return token
    } else {
        // Detailed debug for different Keychain errors
        switch status {
        case errSecItemNotFound:
            print("""
            Keychain item not found for token type: \(tokenType).
            Suggestion: Ensure the token was saved to the Keychain correctly.
            Status: \(status)
            """)
        case errSecAuthFailed:
            print("""
            Authentication failed while trying to access Keychain item for token type: \(tokenType).
            Suggestion: Check if the app has proper entitlements for Keychain usage.
            Status: \(status)
            """)
        case errSecDecode:
            print("""
            Unable to decode the Keychain data for token type: \(tokenType).
            Suggestion: Ensure the data being saved and retrieved is properly encoded.
            Status: \(status)
            """)
        default:
            print("""
            Unexpected Keychain error occurred.
            Token type: \(tokenType)
            Status: \(status) (\(SecCopyErrorMessageString(status, nil) ?? "Unknown error" as CFString))
            """)
        }
    }
    return nil
}
struct LoginView: View {
    @State private var email = ""
    @State private var password = ""
    @State private var message = ""
    @State private var jwt: String? = nil
    var onLoginSuccess: () -> Void
    
    private func validateInput() -> Bool {
        if !isValidEmail(email) {
            message = "Invalid email. Must end with @kbtu.kz"
            return false
        }
        
        if !isValidPassword(password) {
            message = "Invalid password. Must be at least 8 characters, include uppercase, lowercase, and a number."
            return false
        }
        
        return true
    }
    
    func encryptPassword(_ password: String, using key: SymmetricKey) -> String? {
        let passwordData = Data(password.utf8)
        
        // Let's assume you are fetching the nonce from somewhere
        let nonceData = getFixedNonce() // Use the fixed nonce data
        
        do {
            // Try to convert nonceData to AES.GCM.Nonce
            let nonce = try AES.GCM.Nonce(data: nonceData)
            
            // Now encrypt the password using the nonce
            let sealedBox = try AES.GCM.seal(passwordData, using: key, nonce: nonce)
            
            return sealedBox.combined?.base64EncodedString()
        } catch {
            print("Encryption failed: \(error)")
            return nil
        }
    }
    
    func generateHashedFingerprint(fingerprint: String) -> String? {
        if let data = fingerprint.data(using: .utf8) {
            let hash = SHA256.hash(data: data)
            return hash.compactMap { String(format: "%02x", $0) }.joined()
        }
        return nil
    }
    
    // Send login request with encrypted password
    func sendLoginRequest(email: String, password: String,fingerprint: String ) {
        guard validateInput() else { return }
        let url = endpoint("api/signin")
        
        // Encrypt the password and prepare the request as before
        guard let key = getKeyFromKeychain(keyIdentifier: "userSymmetricKey") else {
            message = "Error: Key not found"
            return
        }
        guard let encryptedPassword = encryptPassword(password, using: key) else {
            message = "Encryption failed"
            return
        }
        
        guard let hashedFingerprint = generateHashedFingerprint(fingerprint: fingerprint) else {
             message = "Failed to hash fingerprint"
             return
         }
        
        let body: [String: Any] = [
            "email": email,
            "password": encryptedPassword,
            "visitorId" :  hashedFingerprint
        ]
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            message = "Error: Unable to convert body to JSON"
            return
        }
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription)"
                    return
                }
                
                if let data = data {
                    
                    do {
                                           // Attempt to parse the response data
                                           let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any]
                                           print("Parsed Response: \(String(describing: responseObject))") // Debug print to check the parsed data
                                           
                                           if let responseObject = responseObject {
                                               clearKeychainItem(tokenType: "access_token")
                                               clearKeychainItem(tokenType: "refresh_token")
                                               if let accessToken = responseObject["access_token"] as? String,
                                                  let refreshToken = responseObject["refresh_token"] as? String {
                                                   // Save both tokens to Keychain
                                                   if saveJWTToKeychain(token: accessToken, tokenType: "access_token"),
                                                      saveJWTToKeychain(token: refreshToken, tokenType: "refresh_token") {
                                                       message = "Login successful! JWT and refresh token saved securely."
                                                       jwt = accessToken // Update state with the access token
                                                       onLoginSuccess()
                                                       // Proceed with protected request
                                                       APIManager.shared.sendProtectedRequest { protectedResponse in
                                                           print("Fetched Protected Data:")
                                                           print("Name: \(String(describing: protectedResponse?.name))")
                                                           print("Email: \(String(describing: protectedResponse?.email))")
                                                           // Save to model, update UI, or perform other actions
                                                           saveToModel(email: protectedResponse?.email ?? "", name: protectedResponse?.name ?? "")
                                                       }
                                                   } else {
                                                       message = "Login successful, but failed to save JWT and refresh token."
                                                   }
                                               } else {
                                                   // If tokens are not found, print the response and show an error message
                                                   if let errorMessage = responseObject["message"] as? String {
                                                       message = "Login failed: \(errorMessage)"
                                                   } else {
                                                       message = "Login failed: Tokens not found."
                                                   }
                                               }
                                           }
                    } catch {
                        message = "Error: Unable to parse server response. Error details: \(error.localizedDescription)"
                    }
                    // Capture and show the raw server response
                    if let rawResponse = String(data: data, encoding: .utf8) {
                        print("Raw server response: \(rawResponse)") // Debug log
                        message = rawResponse // Update state with the raw response
                    } else {
                        message = "Error: Unable to decode raw server response."
                    }
                } else {
                    message = "Error: No data received from the server."
                }
            }
        }
        
        task.resume()
    }
    

    var body: some View {
        VStack {
            Text("Login")
                .font(.largeTitle)
            
            TextField("Email", text: $email)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            SecureField("Password", text: $password)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
          
            Button(action: {
                sendLoginRequest(email: email, password: password, fingerprint: fingerprint )
          
            }) {
                Text("Login")
                    .fontWeight(.bold)
                    .padding()
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(10)
            }
            .padding()
            
            Text(message)
                .padding()
                .foregroundColor(.green)
        }
        .padding()
    }
}
//struct LoginView_Previews: PreviewProvider {
//    static var previews: some View {
//        LoginView(onLoginSuccess: )
//    }
//}
