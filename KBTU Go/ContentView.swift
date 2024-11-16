//
//  ContentView.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//


import SwiftUI

struct User {
    var name: String
    var email: String
}

func getUserDetails() -> User {
    // In a real scenario, you would decode the JWT or call an API to fetch user details.
    return User(name: "Raiymbek Omarov", email: "raiymbek@example.com")
}

func deleteJWTFromKeychain() -> Bool {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: "userJWT"  // Same identifier
    ]
    
    let status = SecItemDelete(query as CFDictionary)
    return status == errSecSuccess
}

struct ContentView: View {
    @AppStorage("isAuthenticated") private var isAuthenticated: Bool = false
    
    init() {
        // Check for JWT in Keychain
        if let _ = getJWTFromKeychain() {
            isAuthenticated = true
        }
    }
    
    var body: some View {
        Group {
            if isAuthenticated {
                MainView(logoutAction: {
                    let success = deleteJWTFromKeychain()
                    if success {
                        print("JWT deleted successfully")
                    } else {
                        print("Failed to delete JWT")
                    }
                    isAuthenticated = false
                })
            } else {
                AuthenticationView(onAuthenticated: {
                    isAuthenticated = true
                })
            }
        }
    }
}
struct MainView: View {
    @State private var isSheet1Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))]
    @State private var user: User = User(name: "", email: "")
    var logoutAction: () -> Void  // Accept logout closure
    
    var body: some View {
        VStack {
            // Main content
            TabView {
                // First Tab: Rides
                VStack {
                    LazyVGrid(columns: columns, spacing: 16) {
                        // Your ride buttons
                    }
                    .padding()
                    Spacer()
                }
                .tabItem {
                    Label("Rides", systemImage: "car.front.waves.up.fill")
                }
                
                // Second Tab: Profile
                VStack {
                    Text("Name: \(user.name)")
                        .font(.title)
                        .padding()
                    Text("Email: \(user.email)")
                        .font(.subheadline)
                        .foregroundColor(.gray)
                        .padding()
                    
                    Button(action: {
                        logoutAction()  // Log out action
                    }) {
                        Text("Log Out")
                            .font(.headline)
                            .foregroundColor(.red)
                            .padding()
                    }
                }
                .tabItem {
                    Label("Profile", systemImage: "person.crop.circle")
                }
            }
        }
    }
}

struct AuthenticationView: View {
    @State private var showLogin = true
    var onAuthenticated: () -> Void

    var body: some View {
        VStack {
            if showLogin {
                LoginView(onLoginSuccess: onAuthenticated)
            } else {
                RegistrationView(onRegistrationSuccess: onAuthenticated)
            }

            Button(action: {
                showLogin.toggle()
            }) {
                Text(showLogin ? "Switch to Registration" : "Switch to Login")
                    .font(.headline)
                    .padding()
            }
        }
    }
}
struct RideInfoButton: View {
    let peopleAssembled: String
    let destination: String
    let minutesago: String
    let rideName: String
    let status: String
    let color: Color
    let company: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack {
                HStack {
                    Text(destination)
                        .font(.title)
                        .fontWeight(.bold)
                        .foregroundColor(.white)

                    // Add clock icon and "minutes ago" text
                    

                    Text(" \(minutesago) min ago")
                        .font(.subheadline)
                        .foregroundColor(.white)
                    
                    Image(systemName: "clock.arrow.circlepath")
                        .foregroundColor(.white)
                        .font(.subheadline) // Adjust the icon size to match the text
                }

              

               

                Text(rideName)
                    .font(.footnote)
                    .foregroundColor(.white)

                Text(peopleAssembled)
                    .foregroundColor(.white)
        
            }
            .frame(width: 180, height: 180) // Round button size
            .background(
                RoundedRectangle(cornerRadius: 30) // Large corner radius for round shape
                    .fill(color)
                    .opacity(0.7)
            )
            
        }
    }
}

struct SheetContent: View {
    let title: String

    var body: some View {
        VStack {
            Text(title)
                .font(.largeTitle)
            Spacer()
        }
        .padding()
    }
}
struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView()
            .environment(\.locale, .init(identifier: "en")) // Set to English
         
    }
}
