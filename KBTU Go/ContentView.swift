//
//  ContentView.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//


import SwiftUI


struct ContentView: View {
    @State private var isSheet1Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))] // This makes the buttons adapt to the available screen width

    var body: some View {
        TabView {
            // First Tab: Rides
            VStack {
                LazyVGrid(columns: columns, spacing: 16) {
                    // Add your ride info buttons here
                    RideInfoButton(
                        peopleAssembled: "4/4",
                        destination: "Dorm",
                        minutesago: "10",
                        rideName: "Jackie Sayle",
                        status: "Available",
                        color: .yellow, company: "Yandex"
                    ) {
                        isSheet1Presented = true
                    }
                    .sheet(isPresented: $isSheet1Presented) {
                        SheetContent(title: "Ride Details")
                    }

                    RideInfoButton(
                        peopleAssembled: "4/4",
                        destination: "Uni",
                        minutesago: "5",
                        rideName: "Raiymbek Omarov",
                        status: "Available",
                        color: .blue, company: "Indriver"
                    ) {
                        isSheet1Presented = true
                    }
                    .sheet(isPresented: $isSheet1Presented) {
                        SheetContent(title: "Ride Details")
                    }

                    RideInfoButton(
                        peopleAssembled: "3/4",
                        destination: "Uni",
                        minutesago: "5",
                        rideName: "Жұлдыз",
                        status: "Available",
                        color: .purple, company: "Yandex"
                    ) {
                        isSheet1Presented = true
                    }
                    .sheet(isPresented: $isSheet1Presented) {
                        SheetContent(title: "Ride Details")
                    }
                    // Add more buttons if needed
                }
                .padding()

                Spacer() // Push content down
            }
            .tabItem {
                Label("Rides", systemImage: "car.front.waves.up.fill")
            }

            // Second Tab: Profile
            Text("Profile Tab Content")
                .font(.largeTitle)
                .tabItem {
                    Label("Profile", systemImage: "person.crop.circle")
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
#Preview {
    ContentView()
}
