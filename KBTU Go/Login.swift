//
//  Login.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//

import Foundation

import SwiftUI

struct LoginView: View {
    @State private var email = ""
    @State private var password = ""
    @State private var message = ""
    @State private var jwt: String? = nil  // Store JWT if login is successful

    // Function to send email and password to the backend server for login
    func sendLoginRequest(email: String, password: String) {
        // Replace with your server URL
        let url = URL(string: "https://your-backend-server.com/login")!
        
        // Create the JSON payload
        let body: [String: Any] = [
            "email": email,
            "password": password
        ]
        
        // Convert the body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        // Create the URLRequest
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        // Send the request
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription)"
                    return
                }
                
                // Handle server response here
                if let data = data {
                    do {
                        // Assuming the server returns a JWT in the response body
                        let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any]
                        
                        // If the server returns a JWT, save it
                        if let token = responseObject?["token"] as? String {
                            jwt = token
                            message = "Login successful!"
                            print("JWT: \(jwt!)")
                        } else {
                            message = "Login failed: Invalid credentials."
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
            Text("Login")
                .font(.largeTitle)
            
            TextField("Email", text: $email)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            SecureField("Password", text: $password)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            Button(action: {
                sendLoginRequest(email: email, password: password)
            }) {
                Text("Login")
                    .fontWeight(.bold)
                    .padding()
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(10)
            }
            .padding()
            
            if let jwt = jwt {
                Text("JWT: \(jwt)")
                    .padding()
                    .foregroundColor(.green)
            }
            
            Text(message)
                .padding()
                .foregroundColor(.green)
        }
        .padding()
    }
}

struct LoginView_Previews: PreviewProvider {
    static var previews: some View {
        LoginView()
    }
}
