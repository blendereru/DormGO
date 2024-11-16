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

    // Function to send email and password to the backend server
    func sendRegistrationRequest(email: String, password: String) {
        let url = URL(string: "https://1060-95-59-45-33.ngrok-free.app/api/signup")!
        
        // Generate a new symmetric key and store it securely
        // When registering, save the symmetric key:
        let key = SymmetricKey(size: .bits256) // Use a new key for registration
        guard saveKeyToKeychain(key: key, keyIdentifier: "userSymmetricKey") else {
            message = "Error: Unable to save key"
            return
        }
        
        // Encrypt the password with the generated key
        guard let encryptedPassword = encryptPassword(password, using: key) else {
            message = "Encryption failed"
            return
        }
        
        let body: [String: Any] = [
            "email": email,
            "password": encryptedPassword
        ]
        
        // Log the JSON body to see what is being sent
        if let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) {
            if let jsonString = String(data: jsonData, encoding: .utf8) {
                 // Here we print the JSON
                print("reg JSON: \(jsonString)")
            }
        }
        
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
                        let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any]
                        
                        if let token = responseObject?["token"] as? String {
                            if saveJWTToKeychain(token: token) {
                                message = "Registration successful! JWT saved securely."
                                jwt = token // Update state with the JWT
                                onRegistrationSuccess() // Call the correct success handler
                            } else {
                                message = "Registration successful, but failed to save JWT."
                            }
                        } else {
                            message = "Registration failed: Invalid credentials."
                        }
                    } catch {
                        message = "Error: Unable to parse server response."
                    }
                }
            }
        }
        
        task.resume()
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
