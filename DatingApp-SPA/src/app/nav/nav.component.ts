import { Component, OnInit } from '@angular/core';
import { AuthService } from '../_services/auth.service';

@Component({
  selector: 'app-nav',
  templateUrl: './nav.component.html',
  styleUrls: ['./nav.component.css']
})
export class NavComponent implements OnInit {

  // Setting a new empty object of type any
  model: any = {};

  constructor(private authService: AuthService) { }

  ngOnInit() {
  }

login(){
  this.authService.login(this.model).subscribe(next => {
    console.log('Logged in successfully');
  }, error => {
    console.log(error);
  });
}

loggedIn(){
  const token = localStorage.getItem('token');
  return !!token; // Double ! returns as a boolean. Meaning, checks if there's somethingin the 'token', if so, return true. else - false;
}

logOut(){
  localStorage.removeItem('token');
  console.log('Logged out successfully');
}

}
