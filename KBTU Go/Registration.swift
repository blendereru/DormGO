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

func sendProtectedRequest() {
    let url = URL(string: "https://8440-188-127-36-2.ngrok-free.app/api/protected")!
    
    // Retrieve the JWT token from Keychain
    guard let token = getJWTFromKeychain() else {
        print("Error: JWT token not found in Keychain")
        return
    }
    print("JWT Token: \(token)")
    var request = URLRequest(url: url)
     // Assuming the endpoint expects a GET request
    
    // Add the JWT token to the Authorization header
    request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
    
    // Print the headers to terminal
    if let allHeaders = request.allHTTPHeaderFields {
        print("Request Headers: \(allHeaders)")
    }
    
    let task = URLSession.shared.dataTask(with: request) { data, response, error in
        DispatchQueue.main.async {
            if let error = error {
                print("Error: \(error.localizedDescription)")
                return
            }
            
            if let httpResponse = response as? HTTPURLResponse {
                print("HTTP Status Code: \(httpResponse.statusCode)")
                
                if let headers = httpResponse.allHeaderFields as? [String: String] {
                    print("Response Headers: \(headers)")
                }
                
                if let data = data, let responseString = String(data: data, encoding: .utf8) {
                    print("Response Body: \(responseString)")
                } else {
                    print("No data received")
                }
            }
        }
    
    }
    
    task.resume()
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
    func longPollForToken(email:String) {
 
        guard let url = URL(string: "https://8440-188-127-36-2.ngrok-free.app/api/check-confirmation/\(email)") else {
           
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
                            if let token = responseObject["token"] as? String {
                                print("Token received: \(token)")
                                if saveJWTToKeychain(token: token) {
                                    print("JWT saved successfully!")
                                    UserDefaults.standard.set(true, forKey: "isAuthenticated")
                                    sendProtectedRequest() // Proceed with protected request
                                } else {
                                    print("Error saving JWT")
                                    longPollForToken(email:email)
                                }
                            } else {
                                DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
                                    longPollForToken(email:email) // Retry polling
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
        let url = URL(string: "https://8440-188-127-36-2.ngrok-free.app/api/signup")!
        
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
