//
//  Registration.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//

import Foundation
import SwiftUI
import Security
import CryptoKit
struct ProtectedResponse: Codable {
    let email: String
    let name: String
}

struct TokenResponse: Codable {
    let accessToken: String
    let refreshToken: String
}
class APIManager {
    static let shared = APIManager()
    
    func sendProtectedRequest(completion: @escaping (ProtectedResponse?) -> Void) {
        let url = URL(string: "https://ac30-188-127-36-2.ngrok-free.app/api/protected")!
        
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken { success in
                if success {
                    self.sendProtectedRequest(completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Network error: \(error.localizedDescription)")
                    completion(nil)
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken { success in
                            if success {
                                self.sendProtectedRequest(completion: completion)
                            } else {
                                print("Failed to refresh token.")
                                completion(nil)
                            }
                        }
                        return
                    } else if httpResponse.statusCode != 200 {
                        print("Request failed with status code: \(httpResponse.statusCode)")
                        completion(nil)
                        return
                    }
                    
                    if let data = data {
                        do {
                            let decoder = JSONDecoder()
                            let protectedResponse = try decoder.decode(ProtectedResponse.self, from: data)
                            saveToModel(email: protectedResponse.email, name: protectedResponse.name)
                            completion(protectedResponse)
                        } catch {
                            print("Failed to decode response: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received.")
                        completion(nil)
                    }
                }
            }
        }
        task.resume()
    }
    
    func refreshToken(completion: @escaping (Bool) -> Void) {
        // Retrieve tokens from Keychain first
        guard let refreshToken = getJWTFromKeychain(tokenType: "refresh_token"),
              let accessToken = getJWTFromKeychain(tokenType: "access_token") else {
            print("Token(s) missing.")
            completion(false)
            return
        }

        // Prepare the request
        let refreshURL = URL(string: "https://ac30-188-127-36-2.ngrok-free.app/api/refresh-tokens")!
        var request = URLRequest(url: refreshURL)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let body: [String: Any] = [
            "refreshToken": refreshToken,
            "accessToken": accessToken
        ]

        // Set the HTTP body
        request.httpBody = try? JSONSerialization.data(withJSONObject: body, options: [])

        // Make the request to refresh the token
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error refreshing token: \(error.localizedDescription)")
                    completion(false)
                    return
                }

                if let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 {
                    guard let data = data else {
                        print("Error: No data received")
                        completion(false)
                        return
                    }

                    do {
                        if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                            // Extract tokens from the response
                            if let newAccessToken = responseObject["access_token"] as? String,
                               let newRefreshToken = responseObject["refresh_token"] as? String {

                                print("Access Token received: \(newAccessToken)")
                                print("Refresh Token received: \(newRefreshToken)")

                                // Save tokens to Keychain
                                let isAccessTokenSaved = saveJWTToKeychain(token: newAccessToken, tokenType: "access_token")
                                let isRefreshTokenSaved = saveJWTToKeychain(token: newRefreshToken, tokenType: "refresh_token")

                                if isAccessTokenSaved && isRefreshTokenSaved {
                                    print("Both tokens saved successfully!")
                                    completion(true)  // Return true to indicate success
                                } else {
                                    print("Error saving tokens to Keychain")
                                    completion(false)
                                }
                            } else {
                                print("Error: Tokens not found in response")
                                completion(false)
                            }
                        } else {
                            print("Error: Unexpected response format")
                            completion(false)
                        }
                    } catch {
                        print("Error: Failed to parse server response - \(error.localizedDescription)")
                        completion(false)
                    }
                } else {
                    let statusCode = (response as? HTTPURLResponse)?.statusCode ?? -1
                    print("Failed to refresh token. Status code: \(statusCode)")
                    completion(false)
                }
            }
        }
        task.resume()
    }
}





func saveToModel(email: String, name: String) {
    // Save the email and name to your model
    // e.g., update a global object, database, or state
    print("Saved to model: Email = \(email), Name = \(name)")
}
func saveKeyToKeychain(key: SymmetricKey, keyIdentifier: String) -> Bool {
    let keyData = key.withUnsafeBytes { Data($0) }
    
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: keyIdentifier,  // Unique identifier for the key
        kSecValueData as String: keyData
    ]
    
    let status = SecItemAdd(query as CFDictionary, nil)
    
    if status == errSecSuccess {
        return true
    } else if status == errSecDuplicateItem {
        // Update if the item already exists
        let attributesToUpdate: [String: Any] = [kSecValueData as String: keyData]
        let updateStatus = SecItemUpdate(query as CFDictionary, attributesToUpdate as CFDictionary)
        return updateStatus == errSecSuccess
    }
    return false
}

func getKeyFromKeychain(keyIdentifier: String) -> SymmetricKey? {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: keyIdentifier,  // Same identifier used for storage
        kSecReturnData as String: true,
        kSecMatchLimit as String: kSecMatchLimitOne
    ]
    
    var result: AnyObject?
    let status = SecItemCopyMatching(query as CFDictionary, &result)
    
    if status == errSecSuccess, let data = result as? Data {
        return SymmetricKey(data: data)
    }
    return nil
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
}// Decrypt password
func decryptPassword(_ encryptedPassword: String, using key: SymmetricKey) -> String? {
    guard let data = Data(base64Encoded: encryptedPassword) else { return nil }
    do {
        let sealedBox = try AES.GCM.SealedBox(combined: data)
        let decryptedData = try AES.GCM.open(sealedBox, using: key)
        return String(data: decryptedData, encoding: .utf8)
    } catch {
        print("Decryption failed: \(error)")
        return nil
    }
}
// MARK: - Email and Password Validation

func isValidEmail(_ email: String) -> Bool {
    // Check if the email ends with @kbtu.kz
    return email.lowercased().hasSuffix("@kbtu.kz")
}

func isValidPassword(_ password: String) -> Bool {
    // Example password requirements:
    // - Minimum 8 characters
    // - At least one uppercase letter
    // - At least one lowercase letter
    // - At least one number
    let passwordRegEx = "(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9]).{8,}"
    let passwordPredicate = NSPredicate(format: "SELF MATCHES %@", passwordRegEx)
    return passwordPredicate.evaluate(with: password)
}
// MARK: - Registration View

struct RegistrationView: View {
    @State private var email = ""
    @State private var password = ""
    @State private var message = ""
    @State private var jwt: String? = nil // Store JWT token here
    var onRegistrationSuccess: () -> Void // Closure passed for success handling
    
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
    
    func longPollForToken(email:String) {
 
        guard let url = URL(string: "https://ac30-188-127-36-2.ngrok-free.app/api/check-confirmation/\(email)") else {
           
            print("Error: Invalid URL")
            return
        }
        
        let request = URLRequest(url: url)
//        request.httpMethod = "POST" // Set the HTTP method to POST
//
//        // You can further configure the request here, such as adding headers or body
//        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
//        // You can further configure the request here, such as adding headers or body
      
      
        URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }
                
                if let data = data {
                    do {
                        if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                            
                            // Extract tokens
                            if let accessToken = responseObject["access_token"] as? String,
                               let refreshToken = responseObject["refresh_token"] as? String {
                                print("Access Token received: \(accessToken)")
                                print("Refresh Token received: \(refreshToken)")
                                
                                // Save both tokens to Keychain
                                let isAccessTokenSaved = saveJWTToKeychain(token: accessToken, tokenType: "access_token")
                                let isRefreshTokenSaved = saveJWTToKeychain(token: refreshToken, tokenType: "refresh_token")
                                
                                if isAccessTokenSaved && isRefreshTokenSaved {
                                    print("Both tokens saved successfully!")
                                    UserDefaults.standard.set(true, forKey: "isAuthenticated")
                                    
                                    // Proceed with protected request
                                    APIManager.shared.sendProtectedRequest{ protectedResponse in
                                        print("Fetched Protected Data:")
                                        print("Name: \(protectedResponse?.name)")
                                        print("Email: \(protectedResponse?.email)")
                                        
                                        // Save to model, update UI, or perform other actions
                                        saveToModel(email: protectedResponse?.email ?? "", name: protectedResponse?.name ?? "")
                                    }
                                } else {
                                    print("Error saving tokens")
                                    longPollForToken(email: email) // Retry polling for tokens
                                }
                            } else {
                                print("Error: Tokens not found in response")
                                DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
                                    longPollForToken(email: email) // Retry polling
                                }
                            }
                            
                        } else {
                            print("Error: Unexpected response format")
                        }
                    } catch {
                        print("Error: Failed to parse server response - \(error.localizedDescription)")
                    }
                }
            }
        }.resume()
    }
    // Function to send email and password to the backend server
    func sendRegistrationRequest(email: String, password: String) {
        guard validateInput() else { return }
        let url = URL(string: "https://ac30-188-127-36-2.ngrok-free.app/api/signup")!
        
        let key = SymmetricKey(size: .bits256)
        guard saveKeyToKeychain(key: key, keyIdentifier: "userSymmetricKey") else {
            message = "Error: Unable to save key"
            return
        }
        
        guard let encryptedPassword = encryptPassword(password, using: key) else {
            message = "Encryption failed"
            return
        }
        
        let body: [String: Any] = [
            "email": email,
            "password": encryptedPassword
        ]
        
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            message = "Error: Unable to convert body to JSON"
            return
        }
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription)"
                    return
                }
                
                if let data = data {
                    if let rawResponse = String(data: data, encoding: .utf8) {
                         print("Raw Response: \(rawResponse)")
                     }
                    if let httpResponse = response as? HTTPURLResponse {
                        print("HTTP Status Code: \(httpResponse.statusCode)")
                        if httpResponse.statusCode != 200 {
                            print("Unexpected status code: \(httpResponse.statusCode)")
                        }
                    }
                    do {
                        if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                            if responseObject["success"] as? Bool == true {
                                message = "Registration successful. Waiting for token..."
                                longPollForToken(email: email) // Start polling for the token
                            } else {
                                message = "Registration failed: \(responseObject["error"] as? String ?? "Unknown error")"
                                longPollForToken(email:email)
                            }
                        } else {
                            message = "Unexpected response format."
                        }
                    } catch {
                        message = "Error parsing server response."
                    }
                }
            }
        }.resume()
    }
    
    var body: some View {
        VStack {
            Text("Registration")
                .font(.largeTitle)
            
            TextField("Email", text: $email)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            SecureField("Password", text: $password)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            Button(action: {
                sendRegistrationRequest(email: email, password: password)
          
            }) {
                Text("Register")
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
//struct RegistrationView_Previews: PreviewProvider {
//    static var previews: some View {
//        RegistrationView()
//    }
//}
